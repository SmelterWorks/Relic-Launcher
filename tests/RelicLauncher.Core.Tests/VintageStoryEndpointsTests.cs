using FluentAssertions;
using RelicLauncher.Core.Constants;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class VintageStoryEndpointsTests
{
    [Fact]
    public void BuildModDbPageUrl_PrefersUrlAlias()
    {
        var url = VintageStoryEndpoints.BuildModDbPageUrl("carrycapacity", 19);
        url.Should().Be("https://mods.vintagestory.at/carrycapacity");
    }

    [Fact]
    public void BuildModDbPageUrl_FallsBackToHashDetails()
    {
        var url = VintageStoryEndpoints.BuildModDbPageUrl(null, 19);
        url.Should().Be("https://mods.vintagestory.at/#/details/19");
    }
}
