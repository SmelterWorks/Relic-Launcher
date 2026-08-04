using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IGameLocator
{
    Task<Result<GameInstallInfo>> LocateAsync(string? configuredPath, CancellationToken cancellationToken = default);
}
