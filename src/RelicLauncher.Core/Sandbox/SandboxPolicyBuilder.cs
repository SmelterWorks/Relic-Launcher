using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Sandbox;
using RelicLauncher.Core.Server;

namespace RelicLauncher.Core.Sandbox;

public static class SandboxPolicyBuilder
{
    public static SandboxPolicy BuildLauncher(
        AppPaths relicPaths,
        LauncherSettings settings,
        PlatformInfo platform,
        string installPrefix)
    {
        var grants = new List<PathGrant>();

        AddIfValid(grants, relicPaths.RootDirectory, PathAccess.ReadWrite);
        AddIfValid(grants, settings.InstallsRoot ?? platform.DefaultInstallsRoot, PathAccess.ReadWrite);
        AddIfValid(grants, settings.DataPath ?? platform.DefaultDataPath, PathAccess.ReadWrite);
        AddIfValid(grants, settings.ServerDataPath ?? platform.DefaultServerDataPath, PathAccess.ReadWrite);
        AddIfValid(grants, installPrefix, PathAccess.ReadExecute);

        AppendSystemReadGrants(grants, platform.Os);
        AppendLinuxCoreRuntimeGrants(grants);
        if (platform.Os == HostOs.Linux)
        {
            // CoreCLR resolves host and framework assemblies through paths that are hard
            // to enumerate. Read-execute on / keeps writes limited to explicit RW grants.
            AddIfValid(grants, "/", PathAccess.ReadExecute);
        }

        var netGrants = new List<NetPortGrant>
        {
            new()
            {
                Port = 0,
                AllowConnectTcp = true,
                AllowConnectSendUdp = true,
            },
        };

        return new SandboxPolicy
        {
            Kind = SandboxKind.Launcher,
            PathGrants = grants,
            NetPortGrants = netGrants,
            ScopeAbstractUnixSocket = false,
            ScopeSignal = true,
            SeccompProfile = SeccompProfile.Default,
        };
    }

    public static SandboxPolicy BuildGameClient(
        string installsRoot,
        string version,
        string dataPath,
        string? dotNetRoot,
        string installPrefix)
    {
        var grants = new List<PathGrant>();

        AddIfValid(grants, dataPath, PathAccess.ReadWrite);
        var versionDir = GameInstallLayout.GetVersionDirectory(installsRoot, version);
        AddIfValid(grants, versionDir, PathAccess.ReadExecute);
        AddIfValid(grants, installPrefix, PathAccess.ReadExecute);
        AddIfValid(grants, dotNetRoot, PathAccess.ReadExecute);

        AppendGpuDeviceGrants(grants);
        AppendLinuxCoreRuntimeGrants(grants);

        var netGrants = new List<NetPortGrant>
        {
            new()
            {
                Port = 0,
                AllowConnectTcp = true,
                AllowConnectSendUdp = true,
            },
        };

        return new SandboxPolicy
        {
            Kind = SandboxKind.GameClient,
            PathGrants = grants,
            NetPortGrants = netGrants,
            ScopeAbstractUnixSocket = true,
            ScopeSignal = true,
            SeccompProfile = SeccompProfile.Default,
            MaxLandlockAbi = 10,
        };
    }

    public static SandboxPolicy BuildDedicatedServer(
        string installsRoot,
        string version,
        string serverDataPath,
        string? dotNetRoot,
        string installPrefix,
        ushort listenPort)
    {
        var grants = new List<PathGrant>();

        AddIfValid(grants, serverDataPath, PathAccess.ReadWrite);
        var serverDir = GameServerInstallLayout.GetServerDirectory(installsRoot, version);
        AddIfValid(grants, serverDir, PathAccess.ReadExecute);
        AddIfValid(grants, installPrefix, PathAccess.ReadExecute);
        AddIfValid(grants, dotNetRoot, PathAccess.ReadExecute);
        AppendLinuxCoreRuntimeGrants(grants);

        var netGrants = new List<NetPortGrant>
        {
            new()
            {
                Port = listenPort,
                AllowBindTcp = true,
                AllowBindUdp = true,
            },
        };

        return new SandboxPolicy
        {
            Kind = SandboxKind.DedicatedServer,
            PathGrants = grants,
            NetPortGrants = netGrants,
            ScopeAbstractUnixSocket = true,
            ScopeSignal = true,
            SeccompProfile = SeccompProfile.Default,
        };
    }

    public static bool IsPathGranted(SandboxPolicy policy, string absolutePath, PathAccess requiredAccess)
    {
        if (!PathValidator.TryGetFullPath(absolutePath, out var full, out _))
        {
            return false;
        }

        foreach (var grant in policy.PathGrants)
        {
            if (!PathValidator.TryGetFullPath(grant.Path, out var grantFull, out _))
            {
                continue;
            }

            if (!IsUnderPath(full, grantFull))
            {
                continue;
            }

            if (requiredAccess == PathAccess.ReadWrite && grant.Access != PathAccess.ReadWrite)
            {
                continue;
            }

            if (requiredAccess == PathAccess.ReadExecute
                && grant.Access is not PathAccess.ReadExecute and not PathAccess.ReadWrite)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static void AddIfValid(List<PathGrant> grants, string? path, PathAccess access)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!PathValidator.TryGetFullPath(path.Trim(), out var full, out _))
        {
            return;
        }

        grants.Add(new PathGrant { Path = full, Access = access });
    }

    private static void AppendSystemReadGrants(List<PathGrant> grants, HostOs os)
    {
        if (os == HostOs.Linux)
        {
            AddIfValid(grants, "/usr/lib", PathAccess.ReadExecute);
            AddIfValid(grants, "/usr/lib64", PathAccess.ReadExecute);
            AddIfValid(grants, "/lib", PathAccess.ReadExecute);
            AddIfValid(grants, "/lib64", PathAccess.ReadExecute);
            AddIfValid(grants, "/usr/share/dotnet", PathAccess.ReadExecute);
            AppendGpuDeviceGrants(grants);

            var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            AddIfValid(grants, runtimeDir, PathAccess.ReadWrite);
        }
        else if (os == HostOs.Windows)
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            AddIfValid(grants, Path.Combine(programFiles, "dotnet"), PathAccess.ReadExecute);
        }
    }

    private static void AppendGpuDeviceGrants(List<PathGrant> grants)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        AddIfValid(grants, "/dev/dri", PathAccess.ReadWrite);
        AddIfValid(grants, "/dev/null", PathAccess.ReadWrite);
        AddIfValid(grants, "/dev/urandom", PathAccess.ReadOnly);
    }

    private static void AppendLinuxCoreRuntimeGrants(List<PathGrant> grants)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        AddIfValid(grants, Path.GetTempPath(), PathAccess.ReadWrite);
        AddIfValid(grants, "/tmp", PathAccess.ReadWrite);
        AddIfValid(grants, "/etc/ssl", PathAccess.ReadOnly);
        AddIfValid(grants, "/etc/localtime", PathAccess.ReadOnly);
        AddIfValid(grants, "/etc/resolv.conf", PathAccess.ReadOnly);
        AddIfValid(grants, "/etc/fonts", PathAccess.ReadOnly);
        AddIfValid(grants, "/usr/share/fonts", PathAccess.ReadOnly);
        AddIfValid(grants, "/usr/local/share/fonts", PathAccess.ReadOnly);

        var dotNetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        AddIfValid(grants, dotNetRoot, PathAccess.ReadExecute);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        AddIfValid(grants, Path.Combine(home, ".cache"), PathAccess.ReadWrite);
        AddIfValid(grants, Path.Combine(home, ".fontconfig"), PathAccess.ReadWrite);
        AddIfValid(grants, Path.Combine(home, ".Xauthority"), PathAccess.ReadWrite);
    }

    private static bool IsUnderPath(string child, string parent)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var sep = Path.DirectorySeparatorChar.ToString();
        if (!parent.EndsWith(sep, comparison))
        {
            parent += Path.DirectorySeparatorChar;
        }

        return child.StartsWith(parent, comparison)
            || string.Equals(child, parent.TrimEnd(Path.DirectorySeparatorChar), comparison);
    }
}
