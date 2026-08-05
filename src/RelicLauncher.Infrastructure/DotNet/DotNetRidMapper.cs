using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.DotNet;

internal static class DotNetRidMapper
{
    public static string? TryMap(HostOs os, HostArch arch)
        => (os, arch) switch
        {
            (HostOs.Windows, HostArch.X64) => "win-x64",
            (HostOs.Linux, HostArch.X64) => "linux-x64",
            (HostOs.MacOs, HostArch.X64) => "osx-x64",
            (HostOs.MacOs, HostArch.Arm64) => "osx-arm64",
            _ => null,
        };

    public static bool RequiresWindowsDesktop(HostOs os) => os == HostOs.Windows;
}
