using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IUpdateCheckService
{
    Task<Result<LauncherUpdateCheckResult>> CheckForLauncherUpdateAsync(
        LauncherUpdateCheckRequest request,
        CancellationToken cancellationToken = default);
}
