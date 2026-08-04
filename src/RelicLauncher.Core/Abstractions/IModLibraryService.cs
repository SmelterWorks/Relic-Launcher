using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IModLibraryService
{
    Task<Result<IReadOnlyList<LocalModInfo>>> ListInstalledAsync(string dataPath, CancellationToken cancellationToken = default);

    Task<Result<LocalModInfo>> InstallAsync(
        string dataPath,
        ModReleaseInfo release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task<Result> UninstallAsync(LocalModInfo mod, CancellationToken cancellationToken = default);

    Task<Result<LocalModInfo>> SetEnabledAsync(LocalModInfo mod, bool enabled, CancellationToken cancellationToken = default);
}
