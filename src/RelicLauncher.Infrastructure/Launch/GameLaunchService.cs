using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Launch;

public sealed class GameLaunchService : IGameLaunchService
{
    private readonly IProcessRunner _processRunner;
    private readonly IRuntimePlatform _platform;
    private readonly IClientSettingsSessionWriter _sessionWriter;

    public GameLaunchService(
        IProcessRunner processRunner,
        IRuntimePlatform platform,
        IClientSettingsSessionWriter sessionWriter)
    {
        _processRunner = processRunner;
        _platform = platform;
        _sessionWriter = sessionWriter;
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

        var dataPath = string.IsNullOrWhiteSpace(request.DataPath)
            ? _platform.GetPlatformInfo().DefaultDataPath
            : request.DataPath.Trim();
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(GameInstallLayout.GetModsDirectory(dataPath));

        _ = await _sessionWriter.ApplySessionAsync(dataPath, cancellationToken).ConfigureAwait(false);

        var args = new[] { "--dataPath", dataPath };
        return await _processRunner.StartAsync(info.ExecutablePath, args, cancellationToken).ConfigureAwait(false);
    }
}
