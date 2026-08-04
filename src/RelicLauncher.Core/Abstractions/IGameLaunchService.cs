using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IGameLaunchService
{
    Task<Result<GameInstallInfo>> ResolveAsync(GameLaunchRequest request, CancellationToken cancellationToken = default);

    Task<Result> LaunchAsync(GameLaunchRequest request, CancellationToken cancellationToken = default);
}
