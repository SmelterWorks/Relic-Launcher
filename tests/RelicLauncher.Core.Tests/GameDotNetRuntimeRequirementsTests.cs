using FluentAssertions;
using RelicLauncher.Core.Versions;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class GameDotNetRuntimeRequirementsTests
{
    [Theory]
    [InlineData("1.18.8", 7)]
    [InlineData("1.20.12", 7)]
    [InlineData("1.21.0", 8)]
    [InlineData("1.21.6", 8)]
    [InlineData("1.22.0", 10)]
    [InlineData("1.22.0-rc.1", 10)]
    [InlineData("1.22.6", 10)]
    public void TryGetRequiredMajor_MapsGameVersions(string version, int expectedMajor)
    {
        var result = GameDotNetRuntimeRequirements.TryGetRequiredMajor(version);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedMajor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetRequiredMajor_Fails_WhenVersionBlank(string? version)
    {
        var result = GameDotNetRuntimeRequirements.TryGetRequiredMajor(version);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void TryGetRequiredMajor_Fails_ForPreNet7GameVersions()
    {
        var result = GameDotNetRuntimeRequirements.TryGetRequiredMajor("1.18.7");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(".NET Framework");
    }
}
