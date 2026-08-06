using FluentAssertions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Endpoints;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class EndpointProviderTests
{
    [Fact]
    public void Defaults_MatchVintageStoryEndpoints()
    {
        var provider = new EndpointProvider();

        provider.AccountBaseUrl.Should().Be(VintageStoryEndpoints.AccountBaseUrl);
        provider.ModDbApiBaseUrl.Should().Be(VintageStoryEndpoints.ModDbApiBaseUrl);
        provider.ModDbDownloadBaseUrl.Should().Be(VintageStoryEndpoints.ModDbDownloadBaseUrl);
        provider.WikiBaseUrl.Should().Be(VintageStoryEndpoints.WikiBaseUrl);
        provider.BuildModDownloadUrl(42).Should().Be("https://mods.vintagestory.at/download?fileid=42");
    }

    [Fact]
    public void Apply_CustomSettings_UpdatesUrls()
    {
        var provider = new EndpointProvider();
        provider.Apply(new EndpointSettings
        {
            WikiBaseUrl = "https://wiki.example.test/",
            ModDbDownloadBaseUrl = "https://mods.example.test/download",
        });

        provider.WikiBaseUrl.Should().Be("https://wiki.example.test/");
        provider.ModDbDownloadBaseUrl.Should().Be("https://mods.example.test/download");
        provider.BuildModDownloadUrl(7).Should().Be("https://mods.example.test/download?fileid=7");
    }

    [Fact]
    public void Apply_InvalidUrl_FallsBackToDefault()
    {
        var provider = new EndpointProvider();
        provider.Apply(new EndpointSettings { WikiBaseUrl = "not-a-url" });

        provider.WikiBaseUrl.Should().Be(VintageStoryEndpoints.WikiBaseUrl);
    }
}
