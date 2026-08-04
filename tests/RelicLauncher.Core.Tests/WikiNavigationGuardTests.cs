using FluentAssertions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Wiki;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class WikiNavigationGuardTests
{
    private const string DefaultWiki = VintageStoryEndpoints.WikiBaseUrl;

    [Theory]
    [InlineData("https://wiki.vintagestory.at/")]
    [InlineData("https://wiki.vintagestory.at/Copper")]
    [InlineData("/Copper")]
    [InlineData("Copper")]
    [InlineData("api.php?action=query")]
    public void Evaluate_AllowsSameHostPaths(string candidate)
    {
        var decision = WikiNavigationGuard.Evaluate(DefaultWiki, candidate, out var resolved);

        decision.Should().Be(WikiNavigationDecision.Allow);
        resolved.Should().NotBeNull();
        resolved!.Host.Should().Be("wiki.vintagestory.at");
    }

    [Theory]
    [InlineData("https://mods.vintagestory.at/")]
    [InlineData("https://www.vintagestory.at/")]
    [InlineData("https://example.com/")]
    public void Evaluate_OpensExternalHostsOutsideEmbed(string candidate)
    {
        var decision = WikiNavigationGuard.Evaluate(DefaultWiki, candidate, out var resolved);

        decision.Should().Be(WikiNavigationDecision.OpenExternally);
        resolved.Should().NotBeNull();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("file:///etc/passwd")]
    [InlineData("blob:https://wiki.vintagestory.at/abc")]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_BlocksDangerousOrEmptySchemes(string candidate)
    {
        var decision = WikiNavigationGuard.Evaluate(DefaultWiki, candidate, out var resolved);

        decision.Should().Be(WikiNavigationDecision.Block);
        resolved.Should().BeNull();
    }

    [Fact]
    public void Evaluate_BlocksUserInfoOnCandidate()
    {
        var decision = WikiNavigationGuard.Evaluate(
            DefaultWiki,
            "https://user:pass@wiki.vintagestory.at/Copper",
            out _);

        decision.Should().Be(WikiNavigationDecision.Block);
    }

    [Fact]
    public void Evaluate_UsesConfiguredCustomHost()
    {
        var decision = WikiNavigationGuard.Evaluate(
            "https://wiki.example.test/",
            "https://wiki.example.test/Page",
            out var resolved);

        decision.Should().Be(WikiNavigationDecision.Allow);
        resolved!.Host.Should().Be("wiki.example.test");

        WikiNavigationGuard.Evaluate(
                "https://wiki.example.test/",
                "https://wiki.vintagestory.at/",
                out _)
            .Should().Be(WikiNavigationDecision.OpenExternally);
    }

    [Fact]
    public void Evaluate_RejectsHttpWhenBaseIsHttps()
    {
        var decision = WikiNavigationGuard.Evaluate(
            DefaultWiki,
            "http://wiki.vintagestory.at/Copper",
            out _);

        decision.Should().Be(WikiNavigationDecision.Block);
    }

    [Fact]
    public void Evaluate_AllowsHttpWhenBaseIsHttp()
    {
        var decision = WikiNavigationGuard.Evaluate(
            "http://wiki.local/",
            "http://wiki.local/Page",
            out var resolved);

        decision.Should().Be(WikiNavigationDecision.Allow);
        resolved.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_TreatsDifferentPortAsExternal()
    {
        var decision = WikiNavigationGuard.Evaluate(
            "https://wiki.vintagestory.at/",
            "https://wiki.vintagestory.at:8443/",
            out _);

        decision.Should().Be(WikiNavigationDecision.OpenExternally);
    }

    [Fact]
    public void TryParseAbsoluteBase_RejectsInvalidValues()
    {
        WikiNavigationGuard.TryParseAbsoluteBase(null, out _).Should().BeFalse();
        WikiNavigationGuard.TryParseAbsoluteBase("not-a-url", out _).Should().BeFalse();
        WikiNavigationGuard.TryParseAbsoluteBase("ftp://wiki.vintagestory.at/", out _).Should().BeFalse();
        WikiNavigationGuard.TryParseAbsoluteBase("https://user:pass@wiki.vintagestory.at/", out _).Should().BeFalse();
    }
}
