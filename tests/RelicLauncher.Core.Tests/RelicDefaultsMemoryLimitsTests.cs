using FluentAssertions;
using RelicLauncher.Core.Constants;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class RelicDefaultsMemoryLimitsTests
{
    [Fact]
    public void RemoteImageLimits_AreTightened()
    {
        RelicDefaults.MaxRemoteImageBytes.Should().Be(2L * 1024 * 1024);
        RelicDefaults.RemoteImageMemoryCacheEntries.Should().Be(24);
        RelicDefaults.DecodeWidthHomeLogo.Should().Be(800);
        RelicDefaults.DecodeWidthModListLogo.Should().Be(96);
        RelicDefaults.DecodeWidthImageViewer.Should().Be(1280);
    }
}
