using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Platform;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class RuntimePlatformServerTests
{
    [Theory]
    [InlineData(HostOs.Windows, "windowsserver")]
    [InlineData(HostOs.Linux, "linuxserver")]
    [InlineData(HostOs.MacOs, "linuxserver")]
    public void ResolveServerPackageKey_MapsOs(HostOs os, string expected)
    {
        RuntimePlatform.ResolveServerPackageKey(os).Should().Be(expected);
    }

    [Fact]
    public void GetPlatformInfo_IncludesServerFields()
    {
        var info = new RuntimePlatform().GetPlatformInfo();

        info.ServerPackageKey.Should().NotBeNullOrWhiteSpace();
        info.DefaultServerDataPath.Should().NotBeNullOrWhiteSpace();
        info.DefaultServerDataPath.Should().Contain("VintagestoryServerData");
    }
}
