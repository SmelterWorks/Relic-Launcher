using FluentAssertions;
using RelicLauncher.Core.Wiki;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class WikiChallengeDetectorTests
{
    [Theory]
    [InlineData("Just a moment...", "text/html", true)]
    [InlineData("<div id=\"cf-challenge\"></div>", "text/html", true)]
    [InlineData("Attention Required! | Cloudflare", "text/html", true)]
    [InlineData("cdn-cgi/challenge-platform", "text/html", true)]
    [InlineData("{\"query\":{\"general\":{\"sitename\":\"Wiki\"}}}", "application/json", false)]
    [InlineData("<html><body>Normal wiki page about captcha mods</body></html>", "text/html", false)]
    public void LooksLikeChallenge_DetectsKnownSignals(string body, string contentType, bool expected)
    {
        WikiChallengeDetector.LooksLikeChallenge(body, contentType).Should().Be(expected);
    }
}
