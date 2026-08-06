using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.News;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class NewsCacheStoreTests
{
    [Fact]
    public async Task WriteListAsync_ThenReadListAsync_RoundTripsArticles()
    {
        using var temp = new TempAppPaths();
        var store = new NewsCacheStore(new FixedPathProvider(temp.Paths));
        var articles = new List<NewsArticle>
        {
            new() { Title = "Patch notes", Url = "https://www.vintagestory.at/blog/patch" },
        };

        await store.WriteListAsync(articles);
        var cached = await store.ReadListAsync();

        cached.Should().NotBeNull();
        cached!.Articles.Should().ContainSingle(a => a.Title == "Patch notes");
        cached.CachedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WriteArticleAsync_ThenReadArticleAsync_RoundTripsDetail()
    {
        using var temp = new TempAppPaths();
        var store = new NewsCacheStore(new FixedPathProvider(temp.Paths));
        var detail = new NewsArticleDetail
        {
            Title = "Deep dive",
            Url = "https://www.vintagestory.at/blog/deep-dive",
            Body = "Article body",
            Blocks = [],
        };

        await store.WriteArticleAsync(detail);
        var cached = await store.ReadArticleAsync(detail.Url);

        cached.Should().NotBeNull();
        cached!.Article.Title.Should().Be("Deep dive");
    }

    [Fact]
    public async Task ReadListAsync_WhenMissing_ReturnsNull()
    {
        using var temp = new TempAppPaths();
        var store = new NewsCacheStore(new FixedPathProvider(temp.Paths));

        var cached = await store.ReadListAsync();
        cached.Should().BeNull();
    }
}
