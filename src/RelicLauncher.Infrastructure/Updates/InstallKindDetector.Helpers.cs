using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.Updates;

public sealed partial class InstallKindDetector
{
    private DetectedLauncherInstall DetectWindows(string rid, string? executablePath)
    {
        var installDir = TryReadWindowsInstallLocation();
        if (!string.IsNullOrWhiteSpace(installDir))
        {
            return new DetectedLauncherInstall
            {
                InstallKind = LauncherInstallKind.WindowsNsis,
                Rid = rid,
                InstallDirectory = installDir,
                ExecutablePath = executablePath,
                CanApplyInApp = true,
            };
        }

        var portableDir = GetExecutableDirectory(executablePath);
        return new DetectedLauncherInstall
        {
            InstallKind = LauncherInstallKind.WindowsZip,
            Rid = rid,
            InstallDirectory = portableDir,
            ExecutablePath = executablePath,
            CanApplyInApp = IsDirectoryWritable(portableDir),
        };
    }

    private DetectedLauncherInstall DetectLinux(string rid, string? executablePath)
    {
        if (IsAppImage())
        {
            return new DetectedLauncherInstall
            {
                InstallKind = LauncherInstallKind.LinuxAppImage,
                Rid = rid,
                ExecutablePath = executablePath,
                InstallDirectory = GetExecutableDirectory(executablePath),
                CanApplyInApp = true,
            };
        }

        var packageDir = Environment.GetEnvironmentVariable("RELIC_LAUNCHER_INSTALL_DIR")
            ?? "/usr/lib/relic-launcher";
        if (Directory.Exists(packageDir) && IsSystemPath(packageDir))
        {
            return new DetectedLauncherInstall
            {
                InstallKind = LauncherInstallKind.LinuxPackage,
                Rid = rid,
                InstallDirectory = packageDir,
                ExecutablePath = executablePath,
                CanApplyInApp = false,
            };
        }

        var portableDir = GetExecutableDirectory(executablePath);
        return new DetectedLauncherInstall
        {
            InstallKind = LauncherInstallKind.LinuxPortableTar,
            Rid = rid,
            InstallDirectory = portableDir,
            ExecutablePath = executablePath,
            CanApplyInApp = IsDirectoryWritable(portableDir),
        };
    }

    private DetectedLauncherInstall DetectMacOs(string rid, string? executablePath)
    {
        var bundleDir = FindMacBundleRoot(executablePath);
        return new DetectedLauncherInstall
        {
            InstallKind = LauncherInstallKind.MacOsBundle,
            Rid = rid,
            InstallDirectory = bundleDir,
            ExecutablePath = executablePath,
            CanApplyInApp = !string.IsNullOrWhiteSpace(bundleDir) && IsDirectoryWritable(bundleDir),
        };
    }

    private static DetectedLauncherInstall Create(
        LauncherInstallKind kind,
        string rid,
        string? executablePath,
        bool canApply)
    {
        return new DetectedLauncherInstall
        {
            InstallKind = kind,
            Rid = rid,
            ExecutablePath = executablePath,
            CanApplyInApp = canApply,
        };
    }

    internal static string ResolveRid(HostOs os, HostArch arch)
    {
        return (os, arch) switch
        {
            (HostOs.Windows, _) => "win-x64",
            (HostOs.Linux, HostArch.Arm64) => "linux-arm64",
            (HostOs.Linux, _) => "linux-x64",
            (HostOs.MacOs, HostArch.Arm64) => "osx-arm64",
            (HostOs.MacOs, _) => "osx-x64",
            _ => "linux-x64",
        };
    }

    private static bool IsFlatpak()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLATPAK_ID"));

    private static bool IsAppImage()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPIMAGE"));

    private static string? GetExecutableDirectory(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        return Path.GetDirectoryName(executablePath);
    }

    private static string? FindMacBundleRoot(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        var current = new DirectoryInfo(Path.GetDirectoryName(executablePath) ?? string.Empty);
        while (current is not null)
        {
            if (current.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string? TryReadWindowsInstallLocation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            const string keyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RelicLauncher";
            var value = Microsoft.Win32.Registry.GetValue(keyPath, "InstallLocation", null) as string;
            return string.IsNullOrWhiteSpace(value) ? null : value.TrimEnd('\\', '/');
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSystemPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("/usr/", StringComparison.Ordinal) ||
               normalized.StartsWith("/opt/", StringComparison.Ordinal);
    }

    private static bool IsDirectoryWritable(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        try
        {
            var probe = Path.Combine(directory, $".relic-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
