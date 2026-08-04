using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.News;

internal sealed class NewsCacheStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly string _cacheRoot;

    public NewsCacheStore(IAppPathProvider pathProvider)
    {
        _cacheRoot = Path.Combine(pathProvider.GetPaths().RootDirectory, "cache", "news");
    }

    public async Task<CachedNewsList?> ReadListAsync(CancellationToken cancellationToken = default)
    {
        return await ReadAsync<CachedNewsList>(GetListPath(), cancellationToken).ConfigureAwait(false);
    }

    public Task WriteListAsync(IReadOnlyList<NewsArticle> articles, CancellationToken cancellationToken = default)
    {
        var entry = new CachedNewsList
        {
            CachedAt = DateTimeOffset.UtcNow,
            Articles = articles.ToList(),
        };
        return WriteAsync(GetListPath(), entry, cancellationToken);
    }

    public async Task<CachedNewsArticle?> ReadArticleAsync(string url, CancellationToken cancellationToken = default)
    {
        return await ReadAsync<CachedNewsArticle>(GetArticlePath(url), cancellationToken).ConfigureAwait(false);
    }

    public Task WriteArticleAsync(NewsArticleDetail article, CancellationToken cancellationToken = default)
    {
        var entry = new CachedNewsArticle
        {
            CachedAt = DateTimeOffset.UtcNow,
            Article = article,
        };
        return WriteAsync(GetArticlePath(article.Url), entry, cancellationToken);
    }

    private string GetListPath() => Path.Combine(_cacheRoot, "list.json");

    private string GetArticlePath(string url)
        => Path.Combine(_cacheRoot, "articles", HashUrl(url) + ".json");

    private static string HashUrl(string url)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(bytes[..12]).ToLowerInvariant();
    }

    private async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    internal sealed class CachedNewsList
    {
        public DateTimeOffset CachedAt { get; init; }
        public List<NewsArticle> Articles { get; init; } = [];
    }

    internal sealed class CachedNewsArticle
    {
        public DateTimeOffset CachedAt { get; init; }
        public NewsArticleDetail Article { get; init; } = null!;
    }
}
