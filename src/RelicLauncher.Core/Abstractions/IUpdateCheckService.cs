using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IUpdateCheckService
{
    Task<Result<string?>> CheckForLauncherUpdateAsync(CancellationToken cancellationToken = default);
}
