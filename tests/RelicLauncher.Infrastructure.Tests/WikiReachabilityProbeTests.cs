using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Endpoints;
using RelicLauncher.Infrastructure.Wiki;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class WikiReachabilityProbeTests
{
    [Fact]
    public void Classify_ReturnsReachable_ForSiteInfoJson()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"query":{"general":{"sitename":"Vintage Story Wiki"}}}""",
                Encoding.UTF8,
                "application/json"),
        };

        var result = WikiReachabilityProbe.Classify(response, """{"query":{"general":{"sitename":"Vintage Story Wiki"}}}""");

        result.Status.Should().Be(WikiReachabilityStatus.Reachable);
    }

    [Fact]
    public void Classify_ReturnsAccessBlocked_ForChallengeHtml()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>Just a moment...</html>", Encoding.UTF8, "text/html"),
        };

        var result = WikiReachabilityProbe.Classify(response, "<html>Just a moment...</html>");

        result.Status.Should().Be(WikiReachabilityStatus.AccessBlocked);
    }

    [Fact]
    public void Classify_ReturnsTemporarilyUnavailable_For429()
    {
        using var response = new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("slow down", Encoding.UTF8, "text/plain"),
        };

        var result = WikiReachabilityProbe.Classify(response, "slow down");

        result.Status.Should().Be(WikiReachabilityStatus.TemporarilyUnavailable);
        result.HttpStatusCode.Should().Be(429);
    }

    [Fact]
    public void Classify_ReturnsAccessBlocked_For403()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("no", Encoding.UTF8, "text/plain"),
        };

        var result = WikiReachabilityProbe.Classify(response, "no");

        result.Status.Should().Be(WikiReachabilityStatus.AccessBlocked);
    }

    [Fact]
    public void Classify_ReturnsServerError_For500()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("err", Encoding.UTF8, "text/plain"),
        };

        var result = WikiReachabilityProbe.Classify(response, "err");

        result.Status.Should().Be(WikiReachabilityStatus.ServerError);
    }

    [Fact]
    public async Task ProbeAsync_ReturnsNetworkFailure_WhenRequestThrows()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
        var probe = new WikiReachabilityProbe(
            NullLogger<WikiReachabilityProbe>.Instance,
            new HttpClient(handler),
            new EndpointProvider());

        var result = await probe.ProbeAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(WikiReachabilityStatus.NetworkFailure);
    }

    [Fact]
    public async Task ProbeAsync_ReturnsReachable_WhenApiResponds()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"batchcomplete":true,"query":{"general":{"sitename":"Wiki"}}}""",
                Encoding.UTF8,
                "application/json"),
        });
        var probe = new WikiReachabilityProbe(
            NullLogger<WikiReachabilityProbe>.Instance,
            new HttpClient(handler),
            new EndpointProvider());

        var result = await probe.ProbeAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(WikiReachabilityStatus.Reachable);
    }
}
