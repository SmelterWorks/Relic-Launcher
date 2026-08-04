using System.Runtime.InteropServices;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.Platform;

public sealed class RuntimePlatform : IRuntimePlatform
{
    public PlatformInfo GetPlatformInfo()
    {
        var os = DetectOs();
        var arch = DetectArch();
        return new PlatformInfo
        {
            Os = os,
            Arch = arch,
            ClientPackageKey = ResolveClientPackageKey(os, arch),
            DefaultDataPath = ResolveDefaultDataPath(os),
            DefaultInstallsRoot = ResolveDefaultInstallsRoot(os),
        };
    }

    internal static HostOs DetectOs()
    {
        if (OperatingSystem.IsWindows())
        {
            return HostOs.Windows;
        }

        if (OperatingSystem.IsLinux())
        {
            return HostOs.Linux;
        }

        if (OperatingSystem.IsMacOS())
        {
            return HostOs.MacOs;
        }

        return HostOs.Unknown;
    }

    internal static HostArch DetectArch()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => HostArch.X64,
            Architecture.Arm64 => HostArch.Arm64,
            _ => HostArch.Unknown,
        };
    }

    internal static string ResolveClientPackageKey(HostOs os, HostArch arch)
    {
        return (os, arch) switch
        {
            (HostOs.Windows, _) => "windows",
            (HostOs.Linux, _) => "linux",
            (HostOs.MacOs, HostArch.Arm64) => "mac-arm64",
            (HostOs.MacOs, _) => "mac-x64",
            _ => "linux",
        };
    }

    internal static string ResolveDefaultDataPath(HostOs os)
    {
        return os switch
        {
            HostOs.Windows => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VintagestoryData"),
            HostOs.MacOs => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                "VintagestoryData"),
            _ => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VintagestoryData"),
        };
    }

    internal static string ResolveDefaultInstallsRoot(HostOs os)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return os switch
        {
            HostOs.Windows => Path.Combine(home, "Games", "RelicLauncher", "Vintagestory"),
            HostOs.MacOs => Path.Combine(home, "Games", "RelicLauncher", "Vintagestory"),
            _ => Path.Combine(home, "Games", "RelicLauncher", "Vintagestory"),
        };
    }
}
