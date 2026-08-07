using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Infrastructure.Endpoints;
using RelicLauncher.Infrastructure.Servers;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class MasterServerClientTests
{
    [Fact]
    public void ParseCatalog_ReadsSampleFixture()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "servers-list-sample.json"));
        var catalog = MasterServerClient.ParseCatalog(json);
        catalog.Should().NotBeNull();
        catalog!.Servers.Count.Should().Be(2);
        catalog.Servers[0].IsOfficialTopS.Should().BeTrue();
        catalog.Servers[1].HasPassword.Should().BeTrue();
    }

    [Fact]
    public async Task FetchCatalogAsync_UsesDiskCache_WhenNetworkFails()
    {
        using var temp = new TempAppPaths();
        var endpoints = new EndpointProvider();
        endpoints.Apply(new RelicLauncher.Core.Models.EndpointSettings
        {
            ServerListUrl = "https://proxy.test/list",
        });

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var client = new MasterServerClient(
            new FixedPathProvider(temp.Paths),
            endpoints,
            NullLogger<MasterServerClient>.Instance,
            new HttpClient(handler));

        var json = await File.ReadAllTextAsync(Path.Combine("Fixtures", "servers-list-sample.json"));
        var catalog = MasterServerClient.ParseCatalog(json)!;
        var cacheDir = Path.Combine(temp.Paths.CacheDirectory, "servers");
        Directory.CreateDirectory(cacheDir);
        await File.WriteAllTextAsync(
            Path.Combine(cacheDir, "catalog.json"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                cachedAt = DateTimeOffset.UtcNow,
                servers = catalog.Servers,
            }));

        var result = await client.FetchCatalogAsync(preferCache: false);
        result.IsSuccess.Should().BeTrue();
        result.Value!.FromCache.Should().BeTrue();
        result.Value.IsStale.Should().BeTrue();
        result.Value.Catalog.Servers.Count.Should().Be(2);
    }

    [Fact]
    public async Task FetchCatalogAsync_FallsBackToOfficial_WhenProxyFails()
    {
        using var temp = new TempAppPaths();
        var endpoints = new EndpointProvider();
        endpoints.Apply(new RelicLauncher.Core.Models.EndpointSettings
        {
            ServerListUrl = "https://proxy.test/list",
        });

        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.Host.Contains("proxy.test", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            if (string.Equals(request.RequestUri!.AbsoluteUri, VintageStoryEndpoints.MasterServerListUrl, StringComparison.Ordinal))
            {
                var json = File.ReadAllText(Path.Combine("Fixtures", "servers-list-sample.json"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new MasterServerClient(
            new FixedPathProvider(temp.Paths),
            endpoints,
            NullLogger<MasterServerClient>.Instance,
            new HttpClient(handler));

        var result = await client.FetchCatalogAsync(preferCache: false);
        result.IsSuccess.Should().BeTrue();
        result.Value!.UsedOfficialFallback.Should().BeTrue();
        result.Value.Catalog.Servers.Count.Should().Be(2);
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
