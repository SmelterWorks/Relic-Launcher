using FluentAssertions;
using RelicLauncher.Core.Mods;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class ModInfoJsonParserTests
{
    [Fact]
    public void TryParse_ReadsDependenciesObject()
    {
        var parsed = ModInfoJsonParser.TryParse(
            """
            {
              "modid": "mymod",
              "name": "My Mod",
              "version": "1.2.3",
              "dependencies": {
                "game": "1.20.0",
                "somelib": "2.0.0",
                "anydep": "*",
                "opendep": ""
              }
            }
            """);

        parsed.Should().NotBeNull();
        parsed!.ModId.Should().Be("mymod");
        parsed.Dependencies.Should().HaveCount(4);
        parsed.Dependencies.Should().Contain(d => d.ModId == "game" && d.MinimumVersion == "1.20.0");
        parsed.Dependencies.Should().Contain(d => d.ModId == "somelib" && d.MinimumVersion == "2.0.0");
        parsed.Dependencies.Should().Contain(d => d.ModId == "anydep" && d.AllowsAnyVersion);
        parsed.Dependencies.Should().Contain(d => d.ModId == "opendep" && d.AllowsAnyVersion);
    }

    [Fact]
    public void TryParse_MissingDependencies_ReturnsEmpty()
    {
        var parsed = ModInfoJsonParser.TryParse("""{"modid":"x","name":"X","version":"1.0.0"}""");
        parsed.Should().NotBeNull();
        parsed!.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void TryParse_JunkJson_ReturnsNull()
    {
        ModInfoJsonParser.TryParse("{not json").Should().BeNull();
    }

    [Fact]
    public void TryParse_ArrayDependencies_Ignored()
    {
        var parsed = ModInfoJsonParser.TryParse(
            """{"modid":"x","dependencies":["game"]}""");
        parsed.Should().NotBeNull();
        parsed!.Dependencies.Should().BeEmpty();
    }
}
