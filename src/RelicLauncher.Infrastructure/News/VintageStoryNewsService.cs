using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Endpoints;

namespace RelicLauncher.Infrastructure.News;

public sealed partial class VintageStoryNewsService : IVintageStoryNewsService
{
    private static readonly TimeSpan ListCacheLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ArticleCacheLifetime = TimeSpan.FromHours(24);

    private readonly ILogger<VintageStoryNewsService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IEndpointProvider _endpoints;
    private readonly NewsCacheStore _cacheStore;
    private readonly SemaphoreSlim _listRefreshLock = new(1, 1);
    private IReadOnlyList<NewsArticle>? _memoryListCache;
    private DateTimeOffset _memoryListExpiresAt;
    private readonly ConcurrentDictionary<string, CachedArticleEntry> _memoryArticleCache = new(StringComparer.OrdinalIgnoreCase);

    public VintageStoryNewsService(ILogger<VintageStoryNewsService> logger, IAppPathProvider pathProvider, IEndpointProvider endpoints)
        : this(logger, CreateDefaultHttpClient(), new NewsCacheStore(pathProvider), endpoints)
    {
    }

    internal VintageStoryNewsService(ILogger<VintageStoryNewsService> logger, HttpClient httpClient, NewsCacheStore cacheStore)
        : this(logger, httpClient, cacheStore, new EndpointProvider())
    {
    }

    internal VintageStoryNewsService(ILogger<VintageStoryNewsService> logger, HttpClient httpClient, NewsCacheStore cacheStore, IEndpointProvider endpoints)
    {
        _logger = logger;
        _httpClient = httpClient;
        _endpoints = endpoints;
        _cacheStore = cacheStore;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RelicLauncher", GetUserAgentVersion()));
        }
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        return new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    public async Task<Result<IReadOnlyList<NewsArticle>>> FetchLatestAsync(int maxItems, CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
        {
            return Result<IReadOnlyList<NewsArticle>>.Success(Array.Empty<NewsArticle>());
        }

        if (TryGetFreshMemoryList(out var freshList))
        {
            return Result<IReadOnlyList<NewsArticle>>.Success(freshList.Take(maxItems).ToList());
        }

        var diskList = await _cacheStore.ReadListAsync(cancellationToken).ConfigureAwait(false);
        if (diskList is not null)
        {
            _memoryListCache = diskList.Articles;
            _memoryListExpiresAt = diskList.CachedAt + ListCacheLifetime;

            if (DateTimeOffset.UtcNow < _memoryListExpiresAt)
            {
                return Result<IReadOnlyList<NewsArticle>>.Success(diskList.Articles.Take(maxItems).ToList());
            }

            _ = RefreshListInBackgroundAsync(maxItems);
            return Result<IReadOnlyList<NewsArticle>>.Success(diskList.Articles.Take(maxItems).ToList());
        }

        return await FetchListFromNetworkAsync(maxItems, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<NewsArticleDetail>> FetchArticleAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Result<NewsArticleDetail>.Failure("Article URL is required.");
        }

        if (_memoryArticleCache.TryGetValue(url, out var memoryEntry) &&
            DateTimeOffset.UtcNow < memoryEntry.ExpiresAt)
        {
            return Result<NewsArticleDetail>.Success(memoryEntry.Article);
        }

        var diskArticle = await _cacheStore.ReadArticleAsync(url, cancellationToken).ConfigureAwait(false);
        if (diskArticle is not null)
        {
            RememberArticle(diskArticle.Article, diskArticle.CachedAt + ArticleCacheLifetime);

            if (DateTimeOffset.UtcNow < diskArticle.CachedAt + ArticleCacheLifetime)
            {
                return Result<NewsArticleDetail>.Success(diskArticle.Article);
            }

            _ = RefreshArticleInBackgroundAsync(url);
            return Result<NewsArticleDetail>.Success(diskArticle.Article);
        }

        return await FetchArticleFromNetworkAsync(url, cancellationToken).ConfigureAwait(false);
    }

    private bool TryGetFreshMemoryList(out IReadOnlyList<NewsArticle> articles)
    {
        if (_memoryListCache is not null && DateTimeOffset.UtcNow < _memoryListExpiresAt)
        {
            articles = _memoryListCache;
            return true;
        }

        articles = Array.Empty<NewsArticle>();
        return false;
    }

    private async Task<Result<IReadOnlyList<NewsArticle>>> FetchListFromNetworkAsync(int maxItems, CancellationToken cancellationToken)
    {
        var blogUrl = _endpoints.NewsBlogUrl;

        try
        {
            using var response = await _httpClient.GetAsync(blogUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<NewsArticle>>.Failure($"News request failed with status {(int)response.StatusCode}.");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var articles = ParseArticles(html, maxItems);
            var cachedAt = DateTimeOffset.UtcNow;
            _memoryListCache = articles;
            _memoryListExpiresAt = cachedAt + ListCacheLifetime;
            await _cacheStore.WriteListAsync(articles, cancellationToken).ConfigureAwait(false);
            PrefetchArticles(articles.Take(3));
            return Result<IReadOnlyList<NewsArticle>>.Success(articles);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogWarning(ex, "Failed to fetch Vintage Story news");
            return Result<IReadOnlyList<NewsArticle>>.Failure(ex.Message);
        }
    }

    private async Task RefreshListInBackgroundAsync(int maxItems)
    {
        if (!await _listRefreshLock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await FetchListFromNetworkAsync(maxItems, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Background news refresh failed");
        }
        finally
        {
            _listRefreshLock.Release();
        }
    }

    private async Task<Result<NewsArticleDetail>> FetchArticleFromNetworkAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<NewsArticleDetail>.Failure($"Article request failed with status {(int)response.StatusCode}.");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = ParseArticle(html, url);
            if (parsed is null)
            {
                return Result<NewsArticleDetail>.Failure("Could not parse article content.");
            }

            RememberArticle(parsed, DateTimeOffset.UtcNow + ArticleCacheLifetime);
            await _cacheStore.WriteArticleAsync(parsed, cancellationToken).ConfigureAwait(false);
            return Result<NewsArticleDetail>.Success(parsed);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogWarning(ex, "Failed to fetch Vintage Story article {Url}", url);
            return Result<NewsArticleDetail>.Failure(ex.Message);
        }
    }

    private async Task RefreshArticleInBackgroundAsync(string url)
    {
        try
        {
            await FetchArticleFromNetworkAsync(url, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Background article refresh failed for {Url}", url);
        }
    }

    private void PrefetchArticles(IEnumerable<NewsArticle> articles)
    {
        foreach (var article in articles)
        {
            _ = RefreshArticleInBackgroundAsync(article.Url);
        }
    }

    private void RememberArticle(NewsArticleDetail article, DateTimeOffset expiresAt)
    {
        _memoryArticleCache[article.Url] = new CachedArticleEntry(article, expiresAt);
    }

    public static NewsArticleDetail? ParseArticle(string html, string url)
    {
        var title = TryExtractArticleTitle(html);
        var published = TryExtractArticlePublishedLabel(html);
        var blocks = ParseArticleBlocks(html);
        var body = BuildBodyFromBlocks(blocks);
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body) && blocks.Count == 0)
        {
            return null;
        }

        return new NewsArticleDetail
        {
            Title = title ?? "Vintage Story news",
            Url = url,
            PublishedLabel = published,
            Body = body,
            Blocks = blocks,
        };
    }

    public static IReadOnlyList<NewsContentBlock> ParseArticleBlocks(string html)
    {
        var sectionMatch = ArticleSectionRegex().Match(html);
        if (sectionMatch.Success)
        {
            return ParseBlocksFromSectionHtml(sectionMatch.Groups["body"].Value);
        }

        var jsonMatch = ArticleBodyJsonRegex().Match(html);
        if (jsonMatch.Success)
        {
            var body = UnescapeJsonString(jsonMatch.Groups["body"].Value);
            if (string.IsNullOrWhiteSpace(body))
            {
                return [];
            }

            return [new NewsContentBlock { Kind = NewsContentBlockKind.Text, Text = body }];
        }

        return [];
    }

    public static string ParseArticleBody(string html)
        => BuildBodyFromBlocks(ParseArticleBlocks(html));

    private static string BuildBodyFromBlocks(IReadOnlyList<NewsContentBlock> blocks)
    {
        var textBlocks = blocks
            .Where(block => block.Kind == NewsContentBlockKind.Text && !string.IsNullOrWhiteSpace(block.Text))
            .Select(block => block.Text!.Trim());

        return string.Join(Environment.NewLine + Environment.NewLine, textBlocks);
    }

    private static IReadOnlyList<NewsContentBlock> ParseBlocksFromSectionHtml(string sectionHtml)
    {
        var blocks = new List<NewsContentBlock>();
        var index = 0;

        while (index < sectionHtml.Length)
        {
            index = ProcessNextSectionTag(sectionHtml, blocks, index);
            if (index < 0)
            {
                break;
            }
        }

        return blocks;
    }

    private static int ProcessNextSectionTag(string sectionHtml, List<NewsContentBlock> blocks, int index)
    {
        var next = FindNextTagIndex(sectionHtml, index);
        if (next < 0)
        {
            AddTextBlock(blocks, sectionHtml[index..]);
            return -1;
        }

        if (next > index)
        {
            AddTextBlock(blocks, sectionHtml[index..next]);
        }

        if (sectionHtml.AsSpan(next).StartsWith("<img", StringComparison.OrdinalIgnoreCase))
        {
            var end = sectionHtml.IndexOf('>', next);
            if (end < 0)
            {
                return -1;
            }

            TryAddImageBlock(blocks, sectionHtml, next, sectionHtml[next..(end + 1)]);
            return end + 1;
        }

        if (sectionHtml.AsSpan(next).StartsWith("<iframe", StringComparison.OrdinalIgnoreCase))
        {
            var end = sectionHtml.IndexOf('>', next);
            if (end < 0)
            {
                return -1;
            }

            TryAddVideoBlock(blocks, sectionHtml[next..(end + 1)]);
            return end + 1;
        }

        if (sectionHtml.AsSpan(next).StartsWith("<video", StringComparison.OrdinalIgnoreCase))
        {
            var end = sectionHtml.IndexOf("</video>", next, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
            {
                return -1;
            }

            TryAddVideoBlock(blocks, sectionHtml[next..(end + 8)]);
            return end + 8;
        }

        var close = sectionHtml.IndexOf('>', next);
        return close < 0 ? sectionHtml.Length : close + 1;
    }

    private static int FindNextTagIndex(string html, int start)
    {
        var img = html.IndexOf("<img", start, StringComparison.OrdinalIgnoreCase);
        var iframe = html.IndexOf("<iframe", start, StringComparison.OrdinalIgnoreCase);
        var video = html.IndexOf("<video", start, StringComparison.OrdinalIgnoreCase);

        var candidates = new[] { img, iframe, video }.Where(i => i >= 0).ToList();
        return candidates.Count == 0 ? -1 : candidates.Min();
    }

    private static void TryAddImageBlock(List<NewsContentBlock> blocks, string sectionHtml, int tagIndex, string imgTag)
    {
        var src = ExtractAttribute(imgTag, "src");
        if (string.IsNullOrWhiteSpace(src) || ShouldSkipImage(src, imgTag))
        {
            return;
        }

        var fullUrl = TryFindFullImageUrl(sectionHtml, tagIndex) ?? src;
        blocks.Add(new NewsContentBlock
        {
            Kind = NewsContentBlockKind.Image,
            Url = NormalizeMediaUrl(fullUrl),
            ThumbnailUrl = NormalizeMediaUrl(src),
            Alt = ExtractAttribute(imgTag, "alt"),
        });
    }

    private static void TryAddVideoBlock(List<NewsContentBlock> blocks, string tagHtml)
    {
        var src = ExtractAttribute(tagHtml, "src");
        if (string.IsNullOrWhiteSpace(src))
        {
            src = VideoSourceRegex().Match(tagHtml).Groups["src"].Value;
        }

        if (string.IsNullOrWhiteSpace(src))
        {
            return;
        }

        var normalized = NormalizeMediaUrl(src);
        blocks.Add(new NewsContentBlock
        {
            Kind = NewsContentBlockKind.Video,
            Url = normalized,
            ThumbnailUrl = TryGetVideoThumbnail(normalized),
            Alt = "Video",
        });
    }

    private static string? TryFindFullImageUrl(string sectionHtml, int tagIndex)
    {
        var searchStart = Math.Max(0, tagIndex - 400);
        var slice = sectionHtml[searchStart..tagIndex];
        var attachMatch = AttachLinkRegex().Match(slice);
        if (!attachMatch.Success)
        {
            return null;
        }

        return attachMatch.Groups["href"].Value;
    }

    private static bool ShouldSkipImage(string src, string imgTag)
    {
        if (src.Contains("/reactions/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (src.Contains(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var alt = ExtractAttribute(imgTag, "alt") ?? string.Empty;
        if (alt.Contains("Like", StringComparison.OrdinalIgnoreCase) ||
            alt.Contains("Cookie", StringComparison.OrdinalIgnoreCase) ||
            alt.Contains("Confused", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static void AddTextBlock(List<NewsContentBlock> blocks, string htmlFragment)
    {
        var text = HtmlToPlainText(htmlFragment);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (blocks.Count > 0 &&
            blocks[^1].Kind == NewsContentBlockKind.Text &&
            blocks[^1].Text is not null)
        {
            blocks[^1] = new NewsContentBlock
            {
                Kind = NewsContentBlockKind.Text,
                Text = blocks[^1].Text + Environment.NewLine + Environment.NewLine + text,
            };
            return;
        }

        blocks.Add(new NewsContentBlock
        {
            Kind = NewsContentBlockKind.Text,
            Text = text,
        });
    }

    private static string? TryGetVideoThumbnail(string videoUrl)
    {
        var youtubeMatch = YoutubeIdRegex().Match(videoUrl);
        if (youtubeMatch.Success)
        {
            return $"https://img.youtube.com/vi/{youtubeMatch.Groups["id"].Value}/hqdefault.jpg";
        }

        return null;
    }

    private static string NormalizeMediaUrl(string url)
    {
        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + url;
        }

        return url;
    }

    private static string? ExtractAttribute(string tag, string attributeName)
    {
        var match = AttributeRegex(attributeName).Match(tag);
        return match.Success ? HtmlDecode(match.Groups["value"].Value.Trim()) : null;
    }

    private static Regex AttributeRegex(string attributeName)
        => new($@"{attributeName}\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)')", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500));

    private static string? TryExtractArticleTitle(string html)
    {
        var headlineMatch = ArticleHeadlineJsonRegex().Match(html);
        if (headlineMatch.Success)
        {
            return UnescapeJsonString(headlineMatch.Groups["title"].Value);
        }

        var match = ArticleTitleSpanRegex().Match(html);
        if (!match.Success)
        {
            return null;
        }

        return HtmlDecode(StripTags(match.Groups["title"].Value.Trim()));
    }

    private static string? TryExtractArticlePublishedLabel(string html)
    {
        var match = ArticlePublishedRegex().Match(html);
        if (!match.Success)
        {
            return null;
        }

        return HtmlDecode(StripTags(match.Groups["meta"].Value.Trim()));
    }

    private static string HtmlToPlainText(string htmlFragment)
    {
        var withBreaks = htmlFragment
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase);

        var text = HtmlDecode(StripTags(withBreaks));
        var lines = text
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private static string UnescapeJsonString(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<string>($"\"{value}\"") ?? value;
        }
        catch (JsonException)
        {
            return value
                .Replace("\\n", Environment.NewLine, StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal)
                .Replace("\\\"", "\"", StringComparison.Ordinal);
        }
    }

    public static IReadOnlyList<NewsArticle> ParseArticles(string html, int maxItems)
    {
        var articles = new List<NewsArticle>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ArticleTitleRegex().Matches(html))
        {
            var url = HtmlDecode(match.Groups["url"].Value.Trim());
            var title = HtmlDecode(StripTags(match.Groups["title"].Value.Trim()));
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            if (!seenUrls.Add(url))
            {
                continue;
            }

            var published = TryExtractPublishedLabel(html, match.Index);
            articles.Add(new NewsArticle
            {
                Title = title,
                Url = url,
                PublishedLabel = published,
            });

            if (articles.Count >= maxItems)
            {
                break;
            }
        }

        return articles;
    }

    private static string? TryExtractPublishedLabel(string html, int titleIndex)
    {
        var searchStart = titleIndex;
        var searchEnd = Math.Min(html.Length, titleIndex + 1200);
        var slice = html[searchStart..searchEnd];
        var metaMatch = PublishedMetaRegex().Match(slice);
        if (!metaMatch.Success)
        {
            return null;
        }

        return HtmlDecode(StripTags(metaMatch.Groups["meta"].Value.Trim()));
    }

    private static string StripTags(string value)
        => TagRegex().Replace(value, string.Empty).Trim();

    private static string HtmlDecode(string value)
        => value
            .Replace("&amp;", "&", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&#39;", "'", StringComparison.Ordinal)
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal);

    private static string GetUserAgentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private sealed record CachedArticleEntry(NewsArticleDetail Article, DateTimeOffset ExpiresAt);

    [GeneratedRegex(@"<h2 class='ipsType_pageTitle'>.*?<a href=""(?<url>[^""]+)""[^>]*>\s*(?<title>[^<]+?)\s*</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArticleTitleRegex();

    [GeneratedRegex(@"<p class='ipsType_light ipsType_reset'>\s*By\s*(?<meta>.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PublishedMetaRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TagRegex();

    [GeneratedRegex("<section class=\"ipsType_richText ipsContained ipsType_normal\"[^>]*>(?<body>.*?)</section>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArticleSectionRegex();

    [GeneratedRegex("\"articleBody\"\\s*:\\s*\"(?<body>(?:\\\\.|[^\"\\\\])*)\"", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArticleBodyJsonRegex();

    [GeneratedRegex("\"headline\"\\s*:\\s*\"(?<title>(?:\\\\.|[^\"\\\\])*)\"", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArticleHeadlineJsonRegex();

    [GeneratedRegex("<span class='ipsType_break ipsContained'>(?<title>[^<]+)</span>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArticleTitleSpanRegex();

    [GeneratedRegex("<p class='ipsType_light ipsType_reset'>\\s*By\\s*(?<meta>.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArticlePublishedRegex();

    [GeneratedRegex("class=\"[^\"]*ipsAttachLink_image[^\"]*\"[^>]*href=['\"](?<href>[^'\"]+)['\"]", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AttachLinkRegex();

    [GeneratedRegex("<source[^>]+src=['\"](?<src>[^'\"]+)['\"]", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VideoSourceRegex();

    [GeneratedRegex(@"(?:youtube\.com/embed/|youtu\.be/)(?<id>[\w-]+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex YoutubeIdRegex();
}
