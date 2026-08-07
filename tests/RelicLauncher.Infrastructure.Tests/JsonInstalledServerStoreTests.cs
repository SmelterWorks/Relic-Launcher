using System.Globalization;
using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Infrastructure.Server;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class JsonInstalledServerStoreTests
{
    [Fact]
    public void MergeWithDisk_IncludesVersionOnDiskMissingFromInventory()
    {
        var installsRoot = CreateInstallsRoot();
        var version = "1.22.6";
        var dir = GameServerInstallLayout.GetServerDirectory(installsRoot, version);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "VintagestoryServer.dll"), "stub");
        File.WriteAllText(
            GameServerInstallLayout.GetInventoryPath(installsRoot),
            """[{"version":"1.22.5","installPath":"/tmp","executableFound":true,"installedAt":"2026-01-01T00:00:00Z"}]""");

        var merged = JsonInstalledServerStore.MergeWithDisk(installsRoot,
        [
            new InstalledServerVersion
            {
                Version = "1.22.5",
                InstallPath = "/tmp",
                ExecutableFound = true,
                InstalledAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            },
        ]);

        merged.Select(v => v.Version).Should().Equal("1.22.6", "1.22.5");
    }

    [Fact]
    public async Task ListAsync_MergesInventoryWithDiskScan()
    {
        var installsRoot = CreateInstallsRoot();
        var version = "1.22.6";
        var dir = GameServerInstallLayout.GetServerDirectory(installsRoot, version);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "VintagestoryServer.dll"), "stub");
        await File.WriteAllTextAsync(
            GameServerInstallLayout.GetInventoryPath(installsRoot),
            """[{"version":"1.22.5","installPath":"/tmp","executableFound":true,"installedAt":"2026-01-01T00:00:00Z"}]""");

        var store = new JsonInstalledServerStore();
        var list = await store.ListAsync(installsRoot);

        list.IsSuccess.Should().BeTrue();
        list.Value!.Select(v => v.Version).Should().Equal("1.22.6", "1.22.5");
    }

    private static string CreateInstallsRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "RelicLauncherTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
