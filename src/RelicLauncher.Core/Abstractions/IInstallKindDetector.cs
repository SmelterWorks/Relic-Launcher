using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Abstractions;

public interface IInstallKindDetector
{
    DetectedLauncherInstall Detect();
}
