using FluentAssertions;
using RelicLauncher.Core.Paths;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class GameInstallLayoutTests
{
    [Fact]
    public void GetVersionDirectory_JoinsVersionsFolder()
    {
        var path = GameInstallLayout.GetVersionDirectory("/games/vs", "1.22.6");
        path.Should().Be(Path.Combine("/games/vs", "versions", "1.22.6"));
    }

    [Fact]
    public void GetModsDirectory_AppendsMods()
    {
        GameInstallLayout.GetModsDirectory("/data").Should().Be(Path.Combine("/data", "Mods"));
    }
}
