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
        var relicRoot = Path.Combine(Path.GetTempPath(), "RelicLauncherTest", Guid.NewGuid().ToString("N"));
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
        var platform = new PlatformInfo
        {
            Os = HostOs.Linux,
            Arch = HostArch.X64,
            ClientPackageKey = "linux",
            ServerPackageKey = "linuxserver",
            DefaultDataPath = settings.DataPath!,
            DefaultServerDataPath = settings.DataPath!,
            DefaultInstallsRoot = settings.InstallsRoot!,
        };

        var policy = SandboxPolicyBuilder.BuildLauncher(
            relicPaths,
            settings,
            platform,
            "/usr/lib/relic-launcher");

        SandboxPolicyBuilder.IsPathGranted(policy, settings.InstallsRoot!, PathAccess.ReadWrite).Should().BeTrue();
        SandboxPolicyBuilder.IsPathGranted(policy, relicPaths.RootDirectory, PathAccess.ReadWrite).Should().BeTrue();
    }
}
