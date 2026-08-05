using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Versions;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class JsonInstalledVersionStoreTests
{
    [Fact]
    public async Task ListAsync_ScansVersionsDirectory_WhenInventoryMissing()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var versionDir = Path.Combine(installsRoot, "versions", "1.22.6");
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(Path.Combine(versionDir, "Vintagestory"), "bin");

        var store = new JsonInstalledVersionStore();
        var result = await store.ListAsync(installsRoot);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(v => v.Version == "1.22.6" && v.ExecutableFound);
    }

    [Fact]
    public async Task SaveAndList_RoundTripsInventory()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var store = new JsonInstalledVersionStore();
        var installed = new InstalledGameVersion
        {
            Version = "1.22.6",
            InstallPath = Path.Combine(installsRoot, "versions", "1.22.6"),
            ExecutablePath = Path.Combine(installsRoot, "versions", "1.22.6", "Vintagestory"),
            ExecutableFound = true,
        };

        (await store.SaveAsync(installsRoot, [installed])).IsSuccess.Should().BeTrue();
        var listed = await store.ListAsync(installsRoot);

        listed.IsSuccess.Should().BeTrue();
        listed.Value.Should().ContainSingle(v => v.Version == "1.22.6");
    }

    [Fact]
    public async Task ListAsync_ReturnsEmpty_ForBlankInstallsRoot()
    {
        var store = new JsonInstalledVersionStore();
        var result = await store.ListAsync("   ");
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
