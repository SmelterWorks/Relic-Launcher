using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.News;

public sealed partial class VintageStoryNewsService : IVintageStoryNewsService
{
    private const string BlogUrl = "https://www.vintagestory.at/blog.html/";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(15);

    private readonly ILogger<VintageStoryNewsService> _logger;
    private readonly HttpClient _httpClient;
    private IReadOnlyList<NewsArticle>? _cache;
    private DateTimeOffset _cacheExpiresAt;

    public VintageStoryNewsService(ILogger<VintageStoryNewsService> logger)
        : this(logger, CreateDefaultHttpClient())
    {
    }

    internal VintageStoryNewsService(ILogger<VintageStoryNewsService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RelicLauncher", GetUserAgentVersion()));
        }
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    public async Task<Result<IReadOnlyList<NewsArticle>>> FetchLatestAsync(int maxItems, CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
        {
            return Result<IReadOnlyList<NewsArticle>>.Success(Array.Empty<NewsArticle>());
        }

        if (_cache is not null && DateTimeOffset.UtcNow < _cacheExpiresAt)
        {
            return Result<IReadOnlyList<NewsArticle>>.Success(_cache.Take(maxItems).ToList());
        }

        try
        {
            using var response = await _httpClient.GetAsync(BlogUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<NewsArticle>>.Failure($"News request failed with status {(int)response.StatusCode}.");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var articles = ParseArticles(html, maxItems);
            _cache = articles;
            _cacheExpiresAt = DateTimeOffset.UtcNow.Add(CacheLifetime);
            return Result<IReadOnlyList<NewsArticle>>.Success(articles);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogWarning(ex, "Failed to fetch Vintage Story news");
            return Result<IReadOnlyList<NewsArticle>>.Failure(ex.Message);
        }
    }

    public async Task<Result<NewsArticleDetail>> FetchArticleAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Result<NewsArticleDetail>.Failure("Article URL is required.");
        }

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
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

            return Result<NewsArticleDetail>.Success(parsed);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogWarning(ex, "Failed to fetch Vintage Story article {Url}", url);
            return Result<NewsArticleDetail>.Failure(ex.Message);
        }
    }

    public static NewsArticleDetail? ParseArticle(string html, string url)
    {
        var title = TryExtractArticleTitle(html);
        var published = TryExtractArticlePublishedLabel(html);
        var body = ParseArticleBody(html);
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return new NewsArticleDetail
        {
            Title = title ?? "Vintage Story news",
            Url = url,
            PublishedLabel = published,
            Body = body,
        };
    }

    public static string ParseArticleBody(string html)
    {
        var jsonMatch = ArticleBodyJsonRegex().Match(html);
        if (jsonMatch.Success)
        {
            return UnescapeJsonString(jsonMatch.Groups["body"].Value);
        }

        var sectionMatch = ArticleSectionRegex().Match(html);
        if (sectionMatch.Success)
        {
            return HtmlToPlainText(sectionMatch.Groups["body"].Value);
        }

        return string.Empty;
    }

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

    [GeneratedRegex(@"<h2 class='ipsType_pageTitle'>.*?<a href=""(?<url>[^""]+)""[^>]*>\s*(?<title>[^<]+?)\s*</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArticleTitleRegex();

    [GeneratedRegex(@"<p class='ipsType_light ipsType_reset'>\s*By\s*(?<meta>.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PublishedMetaRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TagRegex();

    [GeneratedRegex("\"articleBody\"\\s*:\\s*\"(?<body>(?:\\\\.|[^\"\\\\])*)\"", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArticleBodyJsonRegex();

    [GeneratedRegex("<section class=\"ipsType_richText ipsContained ipsType_normal\"[^>]*>(?<body>.*?)</section>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArticleSectionRegex();

    [GeneratedRegex("\"headline\"\\s*:\\s*\"(?<title>(?:\\\\.|[^\"\\\\])*)\"", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArticleHeadlineJsonRegex();

    [GeneratedRegex("<span class='ipsType_break ipsContained'>(?<title>[^<]+)</span>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArticleTitleSpanRegex();

    [GeneratedRegex("<p class='ipsType_light ipsType_reset'>\\s*By\\s*(?<meta>.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArticlePublishedRegex();
}
