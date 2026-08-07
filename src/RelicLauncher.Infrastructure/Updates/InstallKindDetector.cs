using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Platform;

namespace RelicLauncher.Infrastructure.Updates;

public sealed partial class InstallKindDetector : IInstallKindDetector
{
    private readonly IRuntimePlatform _platform;

    public InstallKindDetector(IRuntimePlatform platform)
    {
        _platform = platform;
    }

    public DetectedLauncherInstall Detect()
    {
        var platform = _platform.GetPlatformInfo();
        var rid = ResolveRid(platform.Os, platform.Arch);
        var executablePath = Environment.ProcessPath;

        if (IsFlatpak())
        {
            return Create(LauncherInstallKind.Flatpak, rid, executablePath, canApply: false);
        }

        if (OperatingSystem.IsWindows())
        {
            return DetectWindows(rid, executablePath);
        }

        if (OperatingSystem.IsLinux())
        {
            return DetectLinux(rid, executablePath);
        }

        if (OperatingSystem.IsMacOS())
        {
            return DetectMacOs(rid, executablePath);
        }

        return Create(LauncherInstallKind.Unknown, rid, executablePath, canApply: false);
    }
}
