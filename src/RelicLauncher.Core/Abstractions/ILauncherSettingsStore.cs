using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface ILauncherSettingsStore
{
    Task<Result<LauncherSettings>> LoadAsync(CancellationToken cancellationToken = default);
    Task<Result> SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default);
}
