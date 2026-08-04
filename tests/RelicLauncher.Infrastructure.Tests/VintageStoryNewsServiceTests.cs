using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Infrastructure.News;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class VintageStoryNewsServiceTests
{
    [Fact]
    public void ParseArticles_ExtractsTitlesAndUrls_FromLiveLayout()
    {
        var html = """
            <h2 class='ipsType_pageTitle'>
            <a href="https://www.vintagestory.at/blog.html/news/v1226-server-safety-patch-2-r448/" title="Read more about v1.22.6 - Server safety patch #2">
            v1.22.6 - Server safety patch #2
            </a>
            </h2>
            <p class='ipsType_light ipsType_reset'>
            By <a href='https://www.vintagestory.at/profile/2-tyron/'>Tyron</a>, in News, Friday at 12:12 PM
            </p>
            """;

        var articles = VintageStoryNewsService.ParseArticles(html, 5);

        articles.Should().HaveCount(1);
        articles[0].Title.Should().Be("v1.22.6 - Server safety patch #2");
    }

    [Fact]
    public void ParseArticles_ExtractsTitlesAndUrls()
    {
        var articles = VintageStoryNewsService.ParseArticles(VintageStoryNewsHtml.SingleArticle, 5);

        articles.Should().HaveCount(1);
        articles[0].Title.Should().Be("v1.22.6 - Server safety patch #2");
        articles[0].Url.Should().Contain("v1226-server-safety-patch-2");
        articles[0].PublishedLabel.Should().Contain("Tyron");
    }

    [Fact]
    public void ParseArticles_DecodesHtmlEntities()
    {
        var articles = VintageStoryNewsService.ParseArticles(VintageStoryNewsHtml.TwoArticles, 5);

        articles.Should().HaveCount(2);
        articles[0].Title.Should().Be("First & title");
    }

    [Fact]
    public void ParseArticles_RespectsMaxItems()
    {
        var articles = VintageStoryNewsService.ParseArticles(VintageStoryNewsHtml.TwoArticles, 1);

        articles.Should().HaveCount(1);
        articles[0].Title.Should().Be("First & title");
    }

    [Fact]
    public void ParseArticles_SkipsDuplicateUrls()
    {
        var articles = VintageStoryNewsService.ParseArticles(VintageStoryNewsHtml.DuplicateUrls, 5);

        articles.Should().HaveCount(1);
        articles[0].Title.Should().Be("First");
    }

    [Fact]
    public void ParseArticles_ReturnsEmpty_ForUnrelatedHtml()
    {
        var articles = VintageStoryNewsService.ParseArticles("<html><body><h1>News</h1></body></html>", 5);

        articles.Should().BeEmpty();
    }

    [Fact]
    public void ParseArticles_DecodesCommonHtmlEntitiesInTitle()
    {
        var articles = VintageStoryNewsService.ParseArticles(VintageStoryNewsHtml.HtmlEntitiesInTitle, 5);

        articles.Should().HaveCount(1);
        articles[0].Title.Should().Be("Rock & Stone <beta>");
    }

    [Fact]
    public void ParseArticles_SkipsEntriesWithBlankTitleOrUrl()
    {
        var html = """
            <h2 class='ipsType_pageTitle'><a href="https://example.com/ok">Valid</a></h2>
            <h2 class='ipsType_pageTitle'><a href="https://example.com/blank-title">   </a></h2>
            <h2 class='ipsType_pageTitle'><a href="   ">No url</a></h2>
            """;

        var articles = VintageStoryNewsService.ParseArticles(html, 5);

        articles.Should().HaveCount(1);
        articles[0].Title.Should().Be("Valid");
    }

    [Fact]
    public async Task FetchLatestAsync_ReturnsFailure_WhenHttpThrows()
    {
        var handler = new ThrowingHttpMessageHandler();
        var service = new VintageStoryNewsService(NullLogger<VintageStoryNewsService>.Instance, new HttpClient(handler));

        var result = await service.FetchLatestAsync(3);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task FetchLatestAsync_RespectsMaxItemsFromCache()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(VintageStoryNewsHtml.TwoArticles),
        });
        var service = new VintageStoryNewsService(NullLogger<VintageStoryNewsService>.Instance, new HttpClient(handler));

        var first = await service.FetchLatestAsync(5);
        var second = await service.FetchLatestAsync(1);

        first.Value.Should().HaveCount(2);
        second.Value.Should().HaveCount(1);
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("network down");
    }

    [Fact]
    public async Task FetchLatestAsync_ReturnsEmpty_WhenMaxItemsZero()
    {
        var service = new VintageStoryNewsService(NullLogger<VintageStoryNewsService>.Instance);

        var result = await service.FetchLatestAsync(0);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchLatestAsync_ReturnsFailure_WhenHttpStatusNotSuccess()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var service = new VintageStoryNewsService(NullLogger<VintageStoryNewsService>.Instance, new HttpClient(handler));

        var result = await service.FetchLatestAsync(3);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("503");
    }

    [Fact]
    public async Task FetchLatestAsync_ParsesHtmlFromHttpResponse()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(VintageStoryNewsHtml.SingleArticle),
        });
        var service = new VintageStoryNewsService(NullLogger<VintageStoryNewsService>.Instance, new HttpClient(handler));

        var result = await service.FetchLatestAsync(5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Title.Should().Contain("v1.22.6");
    }

    [Fact]
    public async Task FetchLatestAsync_UsesCache_OnSecondCall()
    {
        var callCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(VintageStoryNewsHtml.SingleArticle),
            };
        });
        var service = new VintageStoryNewsService(NullLogger<VintageStoryNewsService>.Instance, new HttpClient(handler));

        await service.FetchLatestAsync(5);
        await service.FetchLatestAsync(5);

        callCount.Should().Be(1);
    }

    [Fact]
    public void ParseArticleBody_ExtractsJsonArticleBody()
    {
        var body = VintageStoryNewsService.ParseArticleBody(VintageStoryNewsHtml.ArticleWithJsonBody);

        body.Should().Contain("Dear players");
        body.Should().Contain("test update");
    }

    [Fact]
    public void ParseArticle_ParsesTitleAndBody_FromJsonLd()
    {
        var article = VintageStoryNewsService.ParseArticle(
            VintageStoryNewsHtml.ArticleWithJsonBody,
            "https://www.vintagestory.at/blog.html/news/test-r1/");

        article.Should().NotBeNull();
        article!.Title.Should().Be("Patch notes");
        article.Body.Should().Contain("Dear players");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
