using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Infrastructure.Endpoints;
using RelicLauncher.Infrastructure.Servers;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class MasterServerClientResilienceTests
{
    [Fact]
    public void ParseCatalog_ReturnsNull_WhenStatusNotOk()
    {
        var catalog = MasterServerClient.ParseCatalog("{\"status\":\"error\",\"data\":[]}");
        catalog.Should().BeNull();
    }

    [Fact]
    public void ParseCatalog_ReturnsEmpty_WhenDataMissing()
    {
        var catalog = MasterServerClient.ParseCatalog("{\"status\":\"ok\"}");
        catalog.Should().BeNull();
    }

    [Fact]
    public async Task FetchCatalogAsync_RejectsOversizedResponse()
    {
        using var temp = new TempAppPaths();
        var oversized = new byte[8 * 1024 * 1024 + 1];
        oversized[0] = (byte)'{';
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(oversized),
        });

        var client = CreateClient(temp, handler);
        var result = await client.FetchCatalogAsync(preferCache: false);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Could not reach the server list");
    }

    [Fact]
    public async Task FetchCatalogAsync_Fails_WhenResponseIsNotValidJson()
    {
        using var temp = new TempAppPaths();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "text/plain"),
        });

        var client = CreateClient(temp, handler);
        var result = await client.FetchCatalogAsync(preferCache: false);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Could not reach the server list");
    }

    [Fact]
    public async Task FetchCatalogAsync_ReturnsFailure_WhenNetworkFailsAndNoCache()
    {
        using var temp = new TempAppPaths();
        var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
        var client = CreateClient(temp, handler);

        var result = await client.FetchCatalogAsync(preferCache: false);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Could not reach the server list");
    }

    private static MasterServerClient CreateClient(TempAppPaths temp, HttpMessageHandler handler)
    {
        return new MasterServerClient(
            new FixedPathProvider(temp.Paths),
            new EndpointProvider(),
            NullLogger<MasterServerClient>.Instance,
            new HttpClient(handler));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
