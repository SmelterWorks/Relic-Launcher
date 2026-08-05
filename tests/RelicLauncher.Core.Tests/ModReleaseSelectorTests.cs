using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class ModReleaseSelectorTests
{
    [Fact]
    public void SelectBest_PrefersHighestModVersionTaggedForGameVersion()
    {
        var releases = new[]
        {
            Release(10, "1.0.0", "1.21.0", "1.22.0"),
            Release(20, "1.2.0", "1.22.0", "1.22.1"),
            Release(30, "1.1.0", "1.22.0"),
            Release(40, "2.0.0", "1.21.0"),
        };

        var best = ModReleaseSelector.SelectBest(releases, "1.22.0");

        best.Should().NotBeNull();
        best!.FileId.Should().Be(20);
        best.ModVersion.Should().Be("1.2.0");
    }

    [Fact]
    public void SelectBest_ReturnsNullWhenNoTagMatches()
    {
        var releases = new[]
        {
            Release(10, "1.0.0", "1.21.0"),
        };

        ModReleaseSelector.SelectBest(releases, "1.22.6").Should().BeNull();
    }

    private static ModReleaseInfo Release(int fileId, string modVersion, params string[] gameVersions)
        => new()
        {
            FileId = fileId,
            ModVersion = modVersion,
            FileName = $"mod_{modVersion}.zip",
            CompatibleGameVersions = gameVersions,
            DownloadUrl = $"https://example.test/download?fileid={fileId}",
        };
}
