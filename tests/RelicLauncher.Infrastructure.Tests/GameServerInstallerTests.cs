using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Infrastructure.Server;
using RelicLauncher.Infrastructure.Versions;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class GameServerInstallerTests
{
    [Fact]
    public void SelectServerPackage_UsesLinuxServerKeyOnLinux()
    {
        using var installer = CreateInstaller();
        var version = CreateVersion("linuxserver", "vs_server_linux-x64_1.22.6.tar.gz");
        var platform = CreatePlatform(HostOs.Linux, "linuxserver");

        var package = installer.SelectServerPackage(version, platform);

        package.Should().NotBeNull();
        package!.PlatformKey.Should().Be("linuxserver");
    }

    [Fact]
    public void SelectServerPackage_UsesWindowsServerKeyOnWindows()
    {
        using var installer = CreateInstaller();
        var version = CreateVersion("windowsserver", "vs_server_win-x64_1.22.6.tar.gz");
        var platform = CreatePlatform(HostOs.Windows, "windowsserver");

        var package = installer.SelectServerPackage(version, platform);

        package.Should().NotBeNull();
        package!.PlatformKey.Should().Be("windowsserver");
    }

    [Fact]
    public void SelectServerPackage_ReturnsNull_WhenMissing()
    {
        using var installer = CreateInstaller();
        var version = CreateVersion("linux", "vs_client_linux-x64_1.22.6.tar.gz");
        var platform = CreatePlatform(HostOs.Linux, "linuxserver");

        installer.SelectServerPackage(version, platform).Should().BeNull();
    }

    [Fact]
    public async Task UninstallAsync_RemovesDirectoryAndStoreEntry()
    {
        var paths = new TempAppPaths();
        var installsRoot = Path.Combine(paths.Paths.CacheDirectory, "installs");
        var version = "1.22.6";
        var dir = GameServerInstallLayout.GetServerDirectory(installsRoot, version);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "VintagestoryServer.dll"), "stub");

        var store = new JsonInstalledServerStore();
        await store.SaveAsync(installsRoot,
        [
            new InstalledServerVersion
            {
                Version = version,
                InstallPath = dir,
                ExecutablePath = Path.Combine(dir, "VintagestoryServer.dll"),
                ExecutableFound = true,
                InstalledAt = DateTimeOffset.UtcNow,
            },
        ]);

        using var installer = CreateInstaller(paths);
        var result = await installer.UninstallAsync(installsRoot, version);

        result.IsSuccess.Should().BeTrue();
        Directory.Exists(dir).Should().BeFalse();
        var list = await store.ListAsync(installsRoot);
        list.Value.Should().BeEmpty();
    }

    private static GameServerInstaller CreateInstaller(TempAppPaths? paths = null)
    {
        paths ??= new TempAppPaths();
        var store = new JsonInstalledServerStore();
        var platform = new FakeRuntimePlatform();
        var clientInstaller = new GameVersionInstaller(
            new FixedPathProvider(paths.Paths),
            new JsonInstalledVersionStore(),
            platform,
            new TestSandboxBrokerClient(),
            NullLogger<GameVersionInstaller>.Instance);
        return new GameServerInstaller(
            new FixedPathProvider(paths.Paths),
            store,
            platform,
            clientInstaller,
            new TestSandboxBrokerClient(),
            NullLogger<GameServerInstaller>.Instance);
    }

    private static GameVersionInfo CreateVersion(string platformKey, string fileName)
        => new()
        {
            Version = "1.22.6",
            Channel = GameVersionChannel.Stable,
            Packages =
            [
                new GameVersionPackage
                {
                    PlatformKey = platformKey,
                    FileName = fileName,
                    CdnUrl = "https://cdn.example/server.tar.gz",
                    Kind = ClientPackageKind.TarGz,
                },
            ],
        };

    private static PlatformInfo CreatePlatform(HostOs os, string serverKey)
        => new()
        {
            Os = os,
            Arch = HostArch.X64,
            ClientPackageKey = os == HostOs.Windows ? "windows" : "linux",
            ServerPackageKey = serverKey,
            DefaultDataPath = "/tmp/data",
            DefaultServerDataPath = "/tmp/server-data",
            DefaultInstallsRoot = "/tmp/installs",
        };
}
