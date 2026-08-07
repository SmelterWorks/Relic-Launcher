using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IModUpdateCheckService
{
    Task<Result<ModUpdateCheckResult>> CheckForUpdatesAsync(
        string dataPath,
        string gameVersion,
        IEnumerable<string> optOutModIds,
        bool force = false,
        CancellationToken cancellationToken = default);
}
