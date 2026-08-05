using System.Globalization;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Mods;

public sealed class ModDependencyInstallPlanner : IModDependencyInstallPlanner
{
    private readonly IModLibraryService _modLibrary;
    private readonly IModReleaseResolver _releaseResolver;
    private readonly ILogger<ModDependencyInstallPlanner> _logger;

    public ModDependencyInstallPlanner(
        IModLibraryService modLibrary,
        IModReleaseResolver releaseResolver,
        ILogger<ModDependencyInstallPlanner> logger)
    {
        _modLibrary = modLibrary;
        _releaseResolver = releaseResolver;
        _logger = logger;
    }

    public async Task<Result<ModDependencyInstallPlan>> PlanAsync(
        ModReleaseInfo rootRelease,
        string gameVersion,
        IReadOnlyList<LocalModInfo> installed,
        CancellationToken cancellationToken = default)
    {
        if (rootRelease.FileId <= 0)
        {
            return Result<ModDependencyInstallPlan>.Failure("Root release has no file id.");
        }

        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            return Result<ModDependencyInstallPlan>.Failure("Game version is required.");
        }

        var cachedRoot = await _modLibrary.EnsureReleaseCachedAsync(rootRelease, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!cachedRoot.IsSuccess)
        {
            return Result<ModDependencyInstallPlan>.Failure(cachedRoot.Error ?? "Could not cache root release.");
        }

        var rootInfo = _modLibrary.TryPeekModInfo(cachedRoot.Value!);
        var rootModId = rootInfo?.ModId;
        if (string.IsNullOrWhiteSpace(rootModId))
        {
            return Result<ModDependencyInstallPlan>.Success(CreateRootOnlyPlan(rootRelease));
        }

        var state = CreateState(rootRelease, rootModId!, rootInfo, installed ?? []);
        await ResolveDepsRecursiveAsync(
            rootModId!,
            rootInfo?.Dependencies ?? [],
            gameVersion.Trim(),
            depth: 1,
            state,
            cancellationToken).ConfigureAwait(false);

        return Result<ModDependencyInstallPlan>.Success(BuildPlan(rootRelease, rootModId!, state));
    }

    private async Task ResolveDepsRecursiveAsync(
        string requiredBy,
        IReadOnlyList<ModDependencyRequirement> dependencies,
        string gameVersion,
        int depth,
        PlanState state,
        CancellationToken cancellationToken)
    {
        if (depth > ModDependencyResolver.DefaultMaxDepth)
        {
            return;
        }

        foreach (var dep in dependencies)
        {
            await ResolveOneDependencyAsync(requiredBy, dep, gameVersion, depth, state, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task ResolveOneDependencyAsync(
        string requiredBy,
        ModDependencyRequirement dep,
        string gameVersion,
        int depth,
        PlanState state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dep.ModId))
        {
            return;
        }

        var modId = dep.ModId.Trim();
        if (BuiltinModIds.IsBuiltin(modId))
        {
            return;
        }

        if (!state.Visiting.Add(modId))
        {
            state.Unresolved.Add(Unresolved(modId, requiredBy, depth, dep.MinimumVersion, "Dependency cycle detected."));
            return;
        }

        try
        {
            var mergedMin = MergeConstraint(state, modId, dep.MinimumVersion);
            if (await TryUseLocalAsync(modId, mergedMin, gameVersion, depth, state, cancellationToken)
                    .ConfigureAwait(false))
            {
                return;
            }

            if (TryReusePlanned(modId, mergedMin, requiredBy, depth, state))
            {
                return;
            }

            await ResolveFromModDbAsync(modId, mergedMin, requiredBy, gameVersion, depth, state, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            state.Visiting.Remove(modId);
        }
    }

    private async Task<bool> TryUseLocalAsync(
        string modId,
        string? mergedMin,
        string gameVersion,
        int depth,
        PlanState state,
        CancellationToken cancellationToken)
    {
        if (!IsSatisfiedLocally(modId, mergedMin, state.InstalledById, out var local) || local is null)
        {
            return false;
        }

        if (state.PeekCache.ContainsKey(modId))
        {
            return true;
        }

        state.PeekCache[modId] = null;
        await ResolveDepsRecursiveAsync(
            modId,
            local.Dependencies,
            gameVersion,
            depth + 1,
            state,
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static bool TryReusePlanned(
        string modId,
        string? mergedMin,
        string requiredBy,
        int depth,
        PlanState state)
    {
        if (!state.Planned.TryGetValue(modId, out var existingStep)
            || existingStep.Release is null
            || !ModVersionComparer.Satisfies(existingStep.Release.ModVersion, mergedMin))
        {
            return false;
        }

        if (existingStep.Depth < depth)
        {
            state.Planned[modId] = new ModDependencyInstallStep
            {
                ModId = modId,
                Release = existingStep.Release,
                RequiredBy = requiredBy,
                Depth = depth,
                MinimumVersion = mergedMin,
            };
        }

        return true;
    }

    private async Task ResolveFromModDbAsync(
        string modId,
        string? mergedMin,
        string requiredBy,
        string gameVersion,
        int depth,
        PlanState state,
        CancellationToken cancellationToken)
    {
        var resolved = await _releaseResolver.ResolveAsync(modId, gameVersion, cancellationToken)
            .ConfigureAwait(false);
        if (!resolved.IsSuccess || resolved.Value is null)
        {
            state.Unresolved.Add(Unresolved(
                modId,
                requiredBy,
                depth,
                mergedMin,
                resolved.Error ?? "Could not resolve release on ModDB."));
            return;
        }

        var release = resolved.Value;
        if (!ModVersionComparer.Satisfies(release.ModVersion, mergedMin))
        {
            state.Unresolved.Add(new ModDependencyInstallStep
            {
                ModId = modId,
                Release = release,
                RequiredBy = requiredBy,
                Depth = depth,
                MinimumVersion = mergedMin,
                IsUnresolved = true,
                Error = $"Resolved {release.ModVersion} does not meet minimum {mergedMin}.",
            });
            return;
        }

        var childDeps = await CacheAndPeekAsync(modId, release, state, cancellationToken).ConfigureAwait(false);
        state.Planned[modId] = new ModDependencyInstallStep
        {
            ModId = modId,
            Release = release,
            RequiredBy = requiredBy,
            Depth = depth,
            MinimumVersion = mergedMin,
        };

        await ResolveDepsRecursiveAsync(
            modId,
            childDeps,
            gameVersion,
            depth + 1,
            state,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ModDependencyRequirement>> CacheAndPeekAsync(
        string modId,
        ModReleaseInfo release,
        PlanState state,
        CancellationToken cancellationToken)
    {
        var cached = await _modLibrary.EnsureReleaseCachedAsync(release, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!cached.IsSuccess)
        {
            _logger.LogDebug("Could not cache dependency {ModId} for peek: {Error}", modId, cached.Error);
            return [];
        }

        var peeked = _modLibrary.TryPeekModInfo(cached.Value!);
        state.PeekCache[modId] = peeked;
        return peeked?.Dependencies ?? [];
    }

    private static string? MergeConstraint(PlanState state, string modId, string? minimum)
    {
        var merged = state.Constraints.TryGetValue(modId, out var existingMin)
            ? ModVersionComparer.TakeHigherMinimum(existingMin, minimum)
            : (ModVersionComparer.IsAnyVersion(minimum) ? null : minimum?.Trim());
        state.Constraints[modId] = merged;
        return merged;
    }

    private static bool IsSatisfiedLocally(
        string modId,
        string? minimumVersion,
        Dictionary<string, LocalModInfo> installedById,
        out LocalModInfo? local)
    {
        if (!installedById.TryGetValue(modId, out local) || !local.IsEnabled)
        {
            local = null;
            return false;
        }

        return ModVersionComparer.Satisfies(local.Version, minimumVersion);
    }

    private static ModDependencyInstallPlan CreateRootOnlyPlan(ModReleaseInfo rootRelease)
        => new()
        {
            RootRelease = rootRelease,
            RootModId = null,
            Steps =
            [
                new ModDependencyInstallStep
                {
                    ModId = rootRelease.FileName
                            ?? rootRelease.FileId.ToString(CultureInfo.InvariantCulture),
                    Release = rootRelease,
                    Depth = 0,
                },
            ],
        };

    private static PlanState CreateState(
        ModReleaseInfo rootRelease,
        string rootModId,
        ParsedModInfo? rootInfo,
        IReadOnlyList<LocalModInfo> installed)
    {
        var state = new PlanState
        {
            InstalledById = BuildInstalledIndex(installed),
        };
        state.PeekCache[rootModId] = rootInfo;
        state.Planned[rootModId] = new ModDependencyInstallStep
        {
            ModId = rootModId,
            Release = rootRelease,
            Depth = 0,
            MinimumVersion = rootInfo?.Version,
        };
        return state;
    }

    private static ModDependencyInstallPlan BuildPlan(
        ModReleaseInfo rootRelease,
        string rootModId,
        PlanState state)
        => new()
        {
            RootRelease = rootRelease,
            RootModId = rootModId,
            Steps = state.Planned.Values
                .OrderByDescending(s => s.Depth)
                .ThenBy(s => s.ModId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Unresolved = state.Unresolved,
        };

    private static ModDependencyInstallStep Unresolved(
        string modId,
        string requiredBy,
        int depth,
        string? minimum,
        string error)
        => new()
        {
            ModId = modId,
            RequiredBy = requiredBy,
            Depth = depth,
            MinimumVersion = minimum,
            IsUnresolved = true,
            Error = error,
        };

    private static Dictionary<string, LocalModInfo> BuildInstalledIndex(IReadOnlyList<LocalModInfo> installed)
    {
        var map = new Dictionary<string, LocalModInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in installed)
        {
            if (string.IsNullOrWhiteSpace(mod.ModId))
            {
                continue;
            }

            if (!map.TryGetValue(mod.ModId, out var existing))
            {
                map[mod.ModId] = mod;
                continue;
            }

            if (mod.IsEnabled && !existing.IsEnabled)
            {
                map[mod.ModId] = mod;
            }
        }

        return map;
    }

    private sealed class PlanState
    {
        public Dictionary<string, LocalModInfo> InstalledById { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ModDependencyInstallStep> Planned { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<ModDependencyInstallStep> Unresolved { get; } = [];
        public Dictionary<string, string?> Constraints { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Visiting { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ParsedModInfo?> PeekCache { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
