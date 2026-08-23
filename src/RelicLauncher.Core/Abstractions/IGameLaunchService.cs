using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IGameLaunchService
{
    bool IsRunning { get; }

    bool IsStopping { get; }

    string? RunningVersion { get; }

    event EventHandler? StateChanged;

    Task<Result<GameInstallInfo>> ResolveAsync(GameLaunchRequest request, CancellationToken cancellationToken = default);

    Task<Result> LaunchAsync(GameLaunchRequest request, CancellationToken cancellationToken = default);

    Task<Result> StopAsync(CancellationToken cancellationToken = default);
}
