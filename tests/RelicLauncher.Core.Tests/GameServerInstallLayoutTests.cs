using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class GameServerInstallLayoutTests
{
    [Fact]
    public void GetServerDirectory_CombinesInstallsRootVersionAndServersFolder()
    {
        var path = GameServerInstallLayout.GetServerDirectory("/games", "1.22.6");
        path.Should().EndWith(Path.Combine("servers", "1.22.6").Replace('\\', Path.DirectorySeparatorChar));
    }

    [Fact]
    public void GetInventoryPath_UsesServersJsonBesideInstallsRoot()
    {
        GameServerInstallLayout.GetInventoryPath("/games")
            .Should().Be(Path.Combine("/games", "servers.json"));
    }

    [Theory]
    [InlineData(HostOs.Windows, "VintagestoryServerData")]
    [InlineData(HostOs.Linux, "VintagestoryServerData")]
    public void ResolveDefaultServerDataPath_EndsWithVintagestoryServerData(HostOs os, string suffix)
    {
        GameServerInstallLayout.ResolveDefaultServerDataPath(os)
            .Should().EndWith(suffix);
    }
}
