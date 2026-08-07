using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface ILauncherUpdateApplyService
{
    bool CanApplyInApp(LauncherInstallKind installKind);

    Task<Result> DownloadAndApplyAsync(
        LauncherUpdateAsset asset,
        DetectedLauncherInstall install,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
