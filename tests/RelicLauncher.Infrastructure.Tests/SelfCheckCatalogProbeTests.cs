using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.SelfCheck;
using RelicLauncher.Infrastructure.SelfCheck;
using RelicLauncher.Infrastructure.Versions;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class SelfCheckCatalogProbeTests
{
    [Fact]
    public async Task RunAsync_Passes_WhenCatalogServiceReturnsVersions()
    {
        using var temp = new TempAppPaths();
        var item = await SelfCheckCatalogProbe.RunAsync(
            () => CreateProvider(temp, CreateCatalogHandler()),
            CancellationToken.None);

        item.Status.Should().Be(SelfCheckStatus.Pass);
        item.Detail.Should().Contain("1.22.6");
    }

    [Fact]
    public async Task RunAsync_UsesDirectFetch_WhenCatalogServiceFails()
    {
        using var temp = new TempAppPaths();
        SelfCheckCatalogProbe.HttpClientFactoryForTests = () => new HttpClient(CreateCatalogHandler());
        try
        {
            var item = await SelfCheckCatalogProbe.RunAsync(
                () => CreateProvider(temp, new StubHandler(_ => throw new HttpRequestException("catalog service offline"))),
                CancellationToken.None);

            item.Status.Should().Be(SelfCheckStatus.Pass);
            item.Detail.Should().Contain("direct fetch");
        }
        finally
        {
            SelfCheckCatalogProbe.HttpClientFactoryForTests = null;
        }
    }

    [Fact]
    public async Task RunAsync_Fails_WhenCatalogAndDirectFetchFail()
    {
        using var temp = new TempAppPaths();
        SelfCheckCatalogProbe.HttpClientFactoryForTests = () =>
            new HttpClient(new StubHandler(_ => throw new HttpRequestException("offline")));
        try
        {
            var item = await SelfCheckCatalogProbe.RunAsync(
                () => CreateProvider(temp, new StubHandler(_ => throw new HttpRequestException("catalog service offline"))),
                CancellationToken.None);

            item.Status.Should().Be(SelfCheckStatus.Fail);
            item.Detail.Should().Contain("catalog service offline");
        }
        finally
        {
            SelfCheckCatalogProbe.HttpClientFactoryForTests = null;
        }
    }

    private static ServiceProvider CreateProvider(TempAppPaths temp, HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Core.Abstractions.IAppPathProvider>(new FixedPathProvider(temp.Paths));
        services.AddLogging();
        services.AddSingleton<Core.Abstractions.IGameVersionCatalog>(_ =>
            new VintageStoryVersionCatalog(
                new FixedPathProvider(temp.Paths),
                NullLogger<VintageStoryVersionCatalog>.Instance,
                new HttpClient(handler)));
        return services.BuildServiceProvider();
    }

    private static StubHandler CreateCatalogHandler()
        => new(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("lateststable", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("1.22.6", Encoding.UTF8, "text/plain"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "1.22.6": {
                        "windows": {
                          "filename": "vs_install_win-x64_1.22.6.exe",
                          "urls": { "cdn": "https://cdn.example/win.exe" }
                        }
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            };
        });
}
