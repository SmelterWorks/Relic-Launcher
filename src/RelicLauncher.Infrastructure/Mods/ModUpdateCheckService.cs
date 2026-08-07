using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Mods;

public sealed class ModUpdateCheckService : IModUpdateCheckService
{
    private static readonly TimeSpan CheckThrottle = TimeSpan.FromHours(24);

    private readonly IModLibraryService _modLibrary;
    private readonly IModReleaseResolver _releaseResolver;
    private readonly IModOriginResolver _originResolver;
    private readonly IModUpdateStateStore _stateStore;
    private readonly ILogger<ModUpdateCheckService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ModUpdateCheckResult? _cachedResult;
    private DateTimeOffset _cachedAt;
    private string? _cachedGameVersion;
    private string? _cachedDataPath;
    private IReadOnlyList<string> _cachedOptOut = [];

    public ModUpdateCheckService(
        IModLibraryService modLibrary,
        IModReleaseResolver releaseResolver,
        IModOriginResolver originResolver,
        IModUpdateStateStore stateStore,
        ILogger<ModUpdateCheckService> logger)
    {
        _modLibrary = modLibrary;
        _releaseResolver = releaseResolver;
        _originResolver = originResolver;
        _stateStore = stateStore;
        _logger = logger;
    }

    public async Task<Result<ModUpdateCheckResult>> CheckForUpdatesAsync(
        string dataPath,
        string gameVersion,
        IEnumerable<string> optOutModIds,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            return Result<ModUpdateCheckResult>.Failure("Data path is not set.");
        }

        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            return Result<ModUpdateCheckResult>.Failure("Active game version is not set.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!force && TryGetCached(dataPath, gameVersion, optOutModIds, out var cached))
            {
                return Result<ModUpdateCheckResult>.Success(cached.WithThrottled(true));
            }

            var result = await CheckInternalAsync(dataPath, gameVersion, optOutModIds, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return result;
            }

            _cachedResult = result.Value;
            _cachedAt = DateTimeOffset.UtcNow;
            _cachedDataPath = dataPath;
            _cachedGameVersion = gameVersion;
            _cachedOptOut = optOutModIds.ToList();
            _stateStore.SetLastCheckUtc(_cachedAt);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool TryGetCached(
        string dataPath,
        string gameVersion,
        IEnumerable<string> optOutModIds,
        out ModUpdateCheckResult result)
    {
        result = null!;
        var lastCheck = _stateStore.GetLastCheckUtc();
        if (lastCheck is null || DateTimeOffset.UtcNow - lastCheck.Value >= CheckThrottle)
        {
            return false;
        }

        if (_cachedResult is null
            || _cachedAt < lastCheck.Value
            || !string.Equals(_cachedDataPath, dataPath, StringComparison.Ordinal)
            || !string.Equals(_cachedGameVersion, gameVersion, StringComparison.Ordinal)
            || !OptOutMatches(_cachedOptOut, optOutModIds))
        {
            return false;
        }

        result = _cachedResult;
        return true;
    }

    private static bool OptOutMatches(IReadOnlyList<string> left, IEnumerable<string> right)
    {
        var rightList = right as IReadOnlyCollection<string> ?? right.ToList();
        if (left.Count != rightList.Count)
        {
            return false;
        }

        var set = new HashSet<string>(left, StringComparer.OrdinalIgnoreCase);
        return rightList.All(set.Contains);
    }

    private async Task<Result<ModUpdateCheckResult>> CheckInternalAsync(
        string dataPath,
        string gameVersion,
        IEnumerable<string> optOutModIds,
        CancellationToken cancellationToken)
    {
        var listResult = await _modLibrary.ListInstalledAsync(dataPath, cancellationToken).ConfigureAwait(false);
        if (!listResult.IsSuccess)
        {
            return Result<ModUpdateCheckResult>.Failure(listResult.Error ?? "Could not list installed mods.");
        }

        var optOut = new HashSet<string>(optOutModIds, StringComparer.OrdinalIgnoreCase);
        var candidates = new List<ModUpdateCandidate>();
        var skippedOptOut = 0;
        var skippedLocal = 0;
        var skippedUnresolved = 0;

        foreach (var mod in listResult.Value!)
        {
            var outcome = await EvaluateModAsync(mod, gameVersion, optOut, cancellationToken).ConfigureAwait(false);
            switch (outcome.Kind)
            {
                case ModUpdateEvaluationKind.Candidate:
                    candidates.Add(outcome.Candidate!);
                    break;
                case ModUpdateEvaluationKind.SkippedOptOut:
                    skippedOptOut++;
                    break;
                case ModUpdateEvaluationKind.SkippedLocal:
                    skippedLocal++;
                    break;
                case ModUpdateEvaluationKind.SkippedUnresolved:
                    skippedUnresolved++;
                    break;
            }
        }

        _logger.LogInformation(
            "Mod update check found {Count} update(s). Skipped opt-out={OptOut}, local={Local}, unresolved={Unresolved}",
            candidates.Count,
            skippedOptOut,
            skippedLocal,
            skippedUnresolved);

        return Result<ModUpdateCheckResult>.Success(new ModUpdateCheckResult
        {
            Candidates = candidates,
            SkippedOptOutCount = skippedOptOut,
            SkippedLocalOnlyCount = skippedLocal,
            SkippedUnresolvedCount = skippedUnresolved,
        });
    }

    private async Task<ModUpdateEvaluation> EvaluateModAsync(
        LocalModInfo mod,
        string gameVersion,
        HashSet<string> optOut,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!mod.IsEnabled || string.IsNullOrWhiteSpace(mod.ModId))
        {
            return ModUpdateEvaluation.None;
        }

        if (optOut.Contains(mod.ModId))
        {
            return ModUpdateEvaluation.OptOut;
        }

        var origin = _originResolver.Resolve(mod);
        if (origin.Source != ModpackModSource.ModDb)
        {
            return ModUpdateEvaluation.Local;
        }

        var resolve = await _releaseResolver.ResolveAsync(mod.ModId, gameVersion, cancellationToken)
            .ConfigureAwait(false);
        if (!resolve.IsSuccess || resolve.Value is null)
        {
            return ModUpdateEvaluation.Unresolved;
        }

        var release = resolve.Value;
        if (!IsUpdateAvailable(mod, origin.FileId, release))
        {
            return ModUpdateEvaluation.None;
        }

        return ModUpdateEvaluation.FromCandidate(new ModUpdateCandidate
        {
            ModId = mod.ModId,
            Name = mod.Name ?? mod.ModId,
            InstalledVersion = mod.Version ?? string.Empty,
            AvailableVersion = release.ModVersion,
            Release = release,
            InstalledFileId = origin.FileId,
        });
    }

    private enum ModUpdateEvaluationKind
    {
        None,
        Candidate,
        SkippedOptOut,
        SkippedLocal,
        SkippedUnresolved,
    }

    private readonly struct ModUpdateEvaluation
    {
        public ModUpdateEvaluationKind Kind { get; init; }
        public ModUpdateCandidate? Candidate { get; init; }

        public static ModUpdateEvaluation None => new() { Kind = ModUpdateEvaluationKind.None };
        public static ModUpdateEvaluation OptOut => new() { Kind = ModUpdateEvaluationKind.SkippedOptOut };
        public static ModUpdateEvaluation Local => new() { Kind = ModUpdateEvaluationKind.SkippedLocal };
        public static ModUpdateEvaluation Unresolved => new() { Kind = ModUpdateEvaluationKind.SkippedUnresolved };

        public static ModUpdateEvaluation FromCandidate(ModUpdateCandidate candidate)
            => new() { Kind = ModUpdateEvaluationKind.Candidate, Candidate = candidate };
    }

    internal static bool IsUpdateAvailable(LocalModInfo mod, int installedFileId, ModReleaseInfo release)
    {
        if (release.FileId > 0 && installedFileId > 0 && release.FileId == installedFileId)
        {
            return false;
        }

        var installedVersion = mod.Version ?? string.Empty;
        var availableVersion = release.ModVersion ?? string.Empty;
        if (string.IsNullOrWhiteSpace(availableVersion))
        {
            return release.FileId > 0 && installedFileId > 0 && release.FileId != installedFileId;
        }

        if (string.IsNullOrWhiteSpace(installedVersion))
        {
            return true;
        }

        return ModVersionComparer.Compare(availableVersion, installedVersion) > 0;
    }
}
