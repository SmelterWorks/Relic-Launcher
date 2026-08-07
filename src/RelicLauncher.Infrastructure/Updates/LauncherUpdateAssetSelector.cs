using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.Updates;

public sealed class LauncherUpdateAssetSelector : ILauncherUpdateAssetSelector
{
    public LauncherUpdateAsset? Select(LauncherUpdateInfo update, DetectedLauncherInstall install)
    {
        var installKindName = ToManifestInstallKind(install.InstallKind);
        var exact = update.Assets.FirstOrDefault(a =>
            string.Equals(a.InstallKind, installKindName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.Rid, install.Rid, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        return update.Assets.FirstOrDefault(a =>
            string.Equals(a.InstallKind, installKindName, StringComparison.OrdinalIgnoreCase));
    }

    internal static string ToManifestInstallKind(LauncherInstallKind installKind)
    {
        return installKind switch
        {
            LauncherInstallKind.WindowsNsis => "WindowsNsis",
            LauncherInstallKind.WindowsZip => "WindowsZip",
            LauncherInstallKind.LinuxPortableTar => "LinuxPortableTar",
            LauncherInstallKind.LinuxAppImage => "LinuxAppImage",
            LauncherInstallKind.LinuxPackage => "LinuxPackage",
            LauncherInstallKind.Flatpak => "Flatpak",
            LauncherInstallKind.MacOsBundle => "MacOsBundle",
            _ => "Unknown",
        };
    }
}
