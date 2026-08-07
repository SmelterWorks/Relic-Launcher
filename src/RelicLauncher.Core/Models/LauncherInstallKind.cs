namespace RelicLauncher.Core.Models;

public enum LauncherInstallKind
{
    Unknown,
    WindowsNsis,
    WindowsZip,
    LinuxPortableTar,
    LinuxAppImage,
    LinuxPackage,
    Flatpak,
    MacOsBundle,
}
