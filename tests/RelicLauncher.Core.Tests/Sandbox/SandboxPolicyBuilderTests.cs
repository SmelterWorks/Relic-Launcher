using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Sandbox;
using Xunit;

namespace RelicLauncher.Core.Tests.Sandbox;

public class SandboxPolicyBuilderTests
{
    [Fact]
    public void BuildGameClient_DeniesRelicSecretsPath()
    {
        var relicRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".relic-sandbox-test",
            Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(Path.GetTempPath(), "VSData", Guid.NewGuid().ToString("N"));
        var installs = Path.Combine(Path.GetTempPath(), "Installs", Guid.NewGuid().ToString("N"));
        var policy = SandboxPolicyBuilder.BuildGameClient(
            installs,
            "1.21.0",
            dataPath,
            null,
            "/usr/lib/relic-launcher");

        var secrets = Path.Combine(relicRoot, "secrets", "account.key");
        SandboxPolicyBuilder.IsPathGranted(policy, dataPath, PathAccess.ReadWrite).Should().BeTrue();
        SandboxPolicyBuilder.IsPathGranted(policy, secrets, PathAccess.ReadOnly).Should().BeFalse();
    }

    [Fact]
    public void BuildDedicatedServer_GrantsServerDataAndBindPort()
    {
        var serverData = Path.Combine(Path.GetTempPath(), "ServerData", Guid.NewGuid().ToString("N"));
        var installs = Path.Combine(Path.GetTempPath(), "Installs", Guid.NewGuid().ToString("N"));
        var policy = SandboxPolicyBuilder.BuildDedicatedServer(
            installs,
            "1.21.0",
            serverData,
            null,
            "/usr/lib/relic-launcher",
            42420);

        policy.NetPortGrants.Should().ContainSingle(g => g.Port == 42420 && g.AllowBindTcp);
        SandboxPolicyBuilder.IsPathGranted(policy, serverData, PathAccess.ReadWrite).Should().BeTrue();
    }

    [Fact]
    public void BuildLauncher_GrantsInstallsRoot()
    {
        var relicRoot = Path.Combine(Path.GetTempPath(), "Relic", Guid.NewGuid().ToString("N"));
        var relicPaths = new AppPaths
        {
            RootDirectory = relicRoot,
            SettingsFile = "settings.json",
            LogsDirectory = "logs",
            ThemesDirectory = "themes",
            CacheDirectory = "cache",
            SecretsDirectory = Path.Combine(relicRoot, "secrets"),
        };
        var settings = new LauncherSettings
        {
            InstallsRoot = Path.Combine(Path.GetTempPath(), "Installs", Guid.NewGuid().ToString("N")),
            DataPath = Path.Combine(Path.GetTempPath(), "Data", Guid.NewGuid().ToString("N")),
        };
        var platform = CreateCurrentPlatform(settings.DataPath!, settings.InstallsRoot!);
        var installPrefix = platform.Os switch
        {
            HostOs.Windows => @"C:\Program Files\Relic Launcher",
            HostOs.MacOs => "/Applications/Relic Launcher.app/Contents/MacOS",
            _ => "/usr/lib/relic-launcher",
        };

        var policy = SandboxPolicyBuilder.BuildLauncher(
            relicPaths,
            settings,
            platform,
            installPrefix);

        SandboxPolicyBuilder.IsPathGranted(policy, settings.InstallsRoot!, PathAccess.ReadWrite).Should().BeTrue();
        SandboxPolicyBuilder.IsPathGranted(policy, relicPaths.RootDirectory, PathAccess.ReadWrite).Should().BeTrue();

        if (OperatingSystem.IsLinux())
        {
            SandboxPolicyBuilder.IsPathGranted(policy, Path.GetTempPath(), PathAccess.ReadWrite).Should().BeTrue();
            SandboxPolicyBuilder.IsPathGranted(policy, "/tmp", PathAccess.ReadWrite).Should().BeTrue();
            SandboxPolicyBuilder.IsPathGranted(policy, "/", PathAccess.ReadExecute).Should().BeTrue();
        }

        policy.ScopeAbstractUnixSocket.Should().BeFalse();
        policy.NetPortGrants.Should().Contain(g => g.Port == 443 && g.AllowConnectTcp);
    }

    private static PlatformInfo CreateCurrentPlatform(string defaultDataPath, string defaultInstallsRoot)
    {
        if (OperatingSystem.IsWindows())
        {
            return new PlatformInfo
            {
                Os = HostOs.Windows,
                Arch = HostArch.X64,
                ClientPackageKey = "windows",
                ServerPackageKey = "windowsserver",
                DefaultDataPath = defaultDataPath,
                DefaultServerDataPath = defaultDataPath,
                DefaultInstallsRoot = defaultInstallsRoot,
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            return new PlatformInfo
            {
                Os = HostOs.MacOs,
                Arch = HostArch.X64,
                ClientPackageKey = "mac-x64",
                ServerPackageKey = "linuxserver",
                DefaultDataPath = defaultDataPath,
                DefaultServerDataPath = defaultDataPath,
                DefaultInstallsRoot = defaultInstallsRoot,
            };
        }

        return new PlatformInfo
        {
            Os = HostOs.Linux,
            Arch = HostArch.X64,
            ClientPackageKey = "linux",
            ServerPackageKey = "linuxserver",
            DefaultDataPath = defaultDataPath,
            DefaultServerDataPath = defaultDataPath,
            DefaultInstallsRoot = defaultInstallsRoot,
        };
    }
}
