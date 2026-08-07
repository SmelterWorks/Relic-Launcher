using FluentAssertions;
using RelicLauncher.Core.Models;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class EndpointSettingsTests
{
    [Fact]
    public void CreateDefaults_MatchesVintageStoryUrls()
    {
        var settings = EndpointSettings.CreateDefaults();

        settings.WikiBaseUrl.Should().Be("https://wiki.vintagestory.at/");
        settings.ServerListUrl.Should().Be("https://smelterworks.com/api/v1/servers/list");
        settings.ModDbDownloadBaseUrl.Should().Be("https://mods.vintagestory.at/download");
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = EndpointSettings.CreateDefaults();
        original.WikiBaseUrl = "https://wiki.example.test/";

        var clone = original.Clone();
        clone.WikiBaseUrl = "https://other.example.test/";

        original.WikiBaseUrl.Should().Be("https://wiki.example.test/");
        clone.WikiBaseUrl.Should().Be("https://other.example.test/");
    }
}
