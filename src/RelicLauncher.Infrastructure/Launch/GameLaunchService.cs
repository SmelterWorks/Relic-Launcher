using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Versions;

namespace RelicLauncher.Infrastructure.Launch;

public sealed class GameLaunchService : IGameLaunchService
{
    private readonly IProcessRunner _processRunner;
    private readonly IRuntimePlatform _platform;
    private readonly IClientSettingsSessionWriter _sessionWriter;
    private readonly IDotNetRuntimeProvisioner _runtimeProvisioner;

    public GameLaunchService(
        IProcessRunner processRunner,
        IRuntimePlatform platform,
        IClientSettingsSessionWriter sessionWriter,
        IDotNetRuntimeProvisioner runtimeProvisioner)
    {
        _processRunner = processRunner;
        _platform = platform;
        _sessionWriter = sessionWriter;
        _runtimeProvisioner = runtimeProvisioner;
    }

    public Task<Result<GameInstallInfo>> ResolveAsync(GameLaunchRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InstallsRoot))
        {
            return Task.FromResult(Result<GameInstallInfo>.Failure("Installs root is not configured. Set it in Settings."));
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Task.FromResult(Result<GameInstallInfo>.Failure("No game version is selected. Install and select one on the Versions page."));
        }

        var installPath = GameInstallLayout.GetVersionDirectory(request.InstallsRoot, request.Version);
        if (!Directory.Exists(installPath))
        {
            return Task.FromResult(Result<GameInstallInfo>.Failure($"Version {request.Version} is not installed."));
        }

        var exe = VintageStoryExecutableLocator.FindClientExecutable(installPath);
        return Task.FromResult(Result<GameInstallInfo>.Success(new GameInstallInfo
        {
            InstallPath = installPath,
            DetectedVersion = request.Version,
            ExecutableFound = exe is not null,
            ExecutablePath = exe,
        }));
    }

    public async Task<Result> LaunchAsync(GameLaunchRequest request, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        if (!resolved.IsSuccess)
        {
            return Result.Failure(resolved.Error ?? "Could not resolve install.");
        }

        var info = resolved.Value!;
        if (!info.ExecutableFound || string.IsNullOrWhiteSpace(info.ExecutablePath))
        {
            return Result.Failure("No client executable found for the selected version.");
        }

        var runtimeMajor = GameDotNetRuntimeRequirements.TryGetRequiredMajor(request.Version);
        if (!runtimeMajor.IsSuccess)
        {
            return Result.Failure(runtimeMajor.Error ?? "Unsupported game version for .NET runtime.");
        }

        var runtime = await _runtimeProvisioner.EnsureAsync(
            runtimeMajor.Value,
            request.Progress,
            cancellationToken).ConfigureAwait(false);
        if (!runtime.IsSuccess)
        {
            return Result.Failure(runtime.Error ?? "Could not provision the required .NET runtime.");
        }

        var dataPath = string.IsNullOrWhiteSpace(request.DataPath)
            ? _platform.GetPlatformInfo().DefaultDataPath
            : request.DataPath.Trim();
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(GameInstallLayout.GetModsDirectory(dataPath));

        _ = await _sessionWriter.ApplySessionAsync(dataPath, cancellationToken).ConfigureAwait(false);

        var args = new[] { "--dataPath", dataPath };
        IReadOnlyDictionary<string, string?>? environment = null;
        if (runtime.Value!.IsManagedByRelic)
        {
            environment = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["DOTNET_ROOT"] = runtime.Value.DotNetRoot,
            };
        }

        return await _processRunner.StartAsync(info.ExecutablePath, args, environment, cancellationToken)
            .ConfigureAwait(false);
    }
}
