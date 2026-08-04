using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Infrastructure.News;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class VintageStoryNewsServiceResilienceTests
{
  [Fact]
  public async Task FetchLatestAsync_ReturnsFailure_WhenBlogHtmlHasNoParsableArticles()
  {
    using var temp = new TempAppPaths();
    var html = """
        <html><body>
        <div class='ipsType_pageTitle'>News</div>
        <p>Layout changed without article links.</p>
        </body></html>
        """;
    var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(html, Encoding.UTF8, "text/html"),
    });
    var service = CreateService(temp, handler);

    var result = await service.FetchLatestAsync(5);

    result.IsSuccess.Should().BeFalse();
    result.Error.Should().Contain("parse");
  }

  [Fact]
  public async Task FetchLatestAsync_ServesDiskCache_WhenNetworkFails()
  {
    using var temp = new TempAppPaths();
    var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(VintageStoryNewsHtml.SingleArticle, Encoding.UTF8, "text/html"),
    });
    var service = CreateService(temp, handler);
    await service.FetchLatestAsync(5);

    var offlineHandler = new StubHandler(_ => throw new HttpRequestException("offline"));
    var offlineService = CreateService(temp, offlineHandler);

    var result = await offlineService.FetchLatestAsync(5);

    result.IsSuccess.Should().BeTrue();
    result.Value.Should().ContainSingle();
  }

  [Fact]
  public void LooksLikeBlogPage_DetectsBlogMarkup()
  {
    var html = new string('x', 250) + "ipsType_pageTitle";

    VintageStoryNewsService.LooksLikeBlogPage(html).Should().BeTrue();
    VintageStoryNewsService.LooksLikeBlogPage("<html><body>short</body></html>").Should().BeFalse();
    VintageStoryNewsService.LooksLikeBlogPage(new string('x', 250) + "blog.html").Should().BeTrue();
  }

  private static VintageStoryNewsService CreateService(TempAppPaths temp, HttpMessageHandler handler)
  {
    var httpClient = new HttpClient(handler);
    var cacheStore = new NewsCacheStore(new FixedPathProvider(temp.Paths));
    return new VintageStoryNewsService(NullLogger<VintageStoryNewsService>.Instance, httpClient, cacheStore);
  }
}
