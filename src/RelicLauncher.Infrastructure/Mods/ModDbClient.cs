using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Mods;

public sealed class ModDbClient : IModDbClient
{
    private const string ApiBase = "https://mods.vintagestory.at/api/";
    private static readonly TimeSpan CatalogTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan DetailsTtl = TimeSpan.FromHours(12);
    private readonly HttpClient _httpClient;
    private readonly IAppPathProvider _pathProvider;
    private readonly ILogger<ModDbClient> _logger;
    private readonly SemaphoreSlim _catalogGate = new(1, 1);
    private IReadOnlyList<ModSummary>? _memoryCatalog;
    private DateTimeOffset _memoryCatalogAt;

    public ModDbClient(IAppPathProvider pathProvider, ILogger<ModDbClient> logger)
        : this(pathProvider, logger, CreateDefaultClient())
    {
    }

    internal ModDbClient(IAppPathProvider pathProvider, ILogger<ModDbClient> logger, HttpClient httpClient)
    {
        _pathProvider = pathProvider;
        _logger = logger;
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RelicLauncher", "0.1.0"));
        }
    }

    public async Task PrefetchCatalogAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCatalogAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ModSearchResult>> SearchAsync(ModSearchQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize <= 0 ? 24 : query.PageSize, 1, 100);
            var resolved = await ResolveSearchSourceAsync(query, cancellationToken).ConfigureAwait(false);
            if (!resolved.IsSuccess)
            {
                return Result<ModSearchResult>.Failure(resolved.Error ?? "Mod search failed.");
            }

            var filtered = FilterAndSort(resolved.Value!.Mods, query);
            var pageItems = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Result<ModSearchResult>.Success(new ModSearchResult
            {
                Mods = pageItems,
                TotalCount = filtered.Count,
                Page = page,
                PageSize = pageSize,
                FromCache = resolved.Value.FromCache,
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            _logger.LogWarning(ex, "ModDB search failed");
            return Result<ModSearchResult>.Failure(ex.Message);
        }
    }

    private async Task<Result<SearchSource>> ResolveSearchSourceAsync(ModSearchQuery query, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.GameVersion) || !string.IsNullOrWhiteSpace(query.Text))
        {
            var remote = await FetchRemoteSearchAsync(query, cancellationToken).ConfigureAwait(false);
            if (remote.IsSuccess)
            {
                return Result<SearchSource>.Success(new SearchSource(remote.Value!, false));
            }

            var fallback = await TryLocalFilterAsync(query, cancellationToken).ConfigureAwait(false);
            return fallback is null
                ? Result<SearchSource>.Failure(remote.Error ?? "Mod search failed.")
                : Result<SearchSource>.Success(new SearchSource(fallback, true));
        }

        var catalog = await EnsureCatalogAsync(forceRefresh: !query.PreferCache, cancellationToken).ConfigureAwait(false);
        return catalog is null
            ? Result<SearchSource>.Failure("Could not load mod catalog.")
            : Result<SearchSource>.Success(new SearchSource(catalog, true));
    }

    private sealed record SearchSource(IReadOnlyList<ModSummary> Mods, bool FromCache);

    public async Task<Result<ModDetails>> GetModAsync(string modIdOrAlias, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modIdOrAlias))
        {
            return Result<ModDetails>.Failure("Mod id is required.");
        }

        var key = modIdOrAlias.Trim();
        var cached = await ReadDetailsCacheAsync(key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return Result<ModDetails>.Success(cached);
        }

        try
        {
            var json = await _httpClient.GetStringAsync($"mod/{Uri.EscapeDataString(key)}", cancellationToken)
                .ConfigureAwait(false);
            var details = ParseDetails(json);
            if (details is null)
            {
                return Result<ModDetails>.Failure("Mod not found.");
            }

            await WriteDetailsCacheAsync(key, details, cancellationToken).ConfigureAwait(false);
            return Result<ModDetails>.Success(details);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            _logger.LogWarning(ex, "ModDB details failed for {Mod}", key);
            return Result<ModDetails>.Failure(ex.Message);
        }
    }

    private async Task<IReadOnlyList<ModSummary>?> TryLocalFilterAsync(ModSearchQuery query, CancellationToken cancellationToken)
    {
        var catalog = await EnsureCatalogAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        return catalog is null ? null : FilterAndSort(catalog, query);
    }

    private async Task<Result<IReadOnlyList<ModSummary>>> FetchRemoteSearchAsync(ModSearchQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var url = BuildSearchUrl(query);
            var json = await _httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<ModSummary>>.Success(ParseSearch(json));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            _logger.LogWarning(ex, "ModDB remote search failed");
            return Result<IReadOnlyList<ModSummary>>.Failure(ex.Message);
        }
    }

    private async Task<IReadOnlyList<ModSummary>?> EnsureCatalogAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        await _catalogGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh &&
                _memoryCatalog is not null &&
                DateTimeOffset.UtcNow - _memoryCatalogAt < CatalogTtl)
            {
                return _memoryCatalog;
            }

            if (!forceRefresh)
            {
                var disk = await ReadCatalogCacheAsync(cancellationToken).ConfigureAwait(false);
                if (disk is not null)
                {
                    _memoryCatalog = disk;
                    _memoryCatalogAt = DateTimeOffset.UtcNow;
                    _ = RefreshCatalogInBackgroundAsync();
                    return disk;
                }
            }

            return await RefreshCatalogCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _catalogGate.Release();
        }
    }

    private async Task RefreshCatalogInBackgroundAsync()
    {
        try
        {
            await _catalogGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_memoryCatalog is not null && DateTimeOffset.UtcNow - _memoryCatalogAt < TimeSpan.FromMinutes(30))
                {
                    return;
                }

                await RefreshCatalogCoreAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _catalogGate.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Background mod catalog refresh failed");
        }
    }

    private async Task<IReadOnlyList<ModSummary>?> RefreshCatalogCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = await _httpClient.GetStringAsync("mods?orderby=downloads", cancellationToken).ConfigureAwait(false);
            var mods = ParseSearch(json);
            _memoryCatalog = mods;
            _memoryCatalogAt = DateTimeOffset.UtcNow;
            await WriteCatalogCacheAsync(mods, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Cached ModDB catalog with {Count} mods", mods.Count);
            return mods;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            _logger.LogWarning(ex, "Failed to refresh ModDB catalog");
            return _memoryCatalog;
        }
    }

    private static List<ModSummary> FilterAndSort(IReadOnlyList<ModSummary> source, ModSearchQuery query)
    {
        IEnumerable<ModSummary> filtered = source;
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            filtered = filtered.Where(m =>
                m.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                (m.Author?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Summary?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.UrlAlias?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false) ||
                m.Tags.Any(t => t.Contains(text, StringComparison.OrdinalIgnoreCase)));
        }

        var orderBy = query.OrderBy ?? "downloads";
        var desc = !string.Equals(query.OrderDirection, "asc", StringComparison.OrdinalIgnoreCase);
        filtered = orderBy.ToLowerInvariant() switch
        {
            "name" => desc
                ? filtered.OrderByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
            "follows" => desc ? filtered.OrderByDescending(m => m.Follows) : filtered.OrderBy(m => m.Follows),
            _ => desc ? filtered.OrderByDescending(m => m.Downloads) : filtered.OrderBy(m => m.Downloads),
        };

        return filtered.ToList();
    }

    public static IReadOnlyList<ModSummary> ParseSearch(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("mods", out var modsEl))
        {
            return [];
        }

        var list = new List<ModSummary>();
        foreach (var mod in modsEl.EnumerateArray())
        {
            list.Add(ParseSummary(mod));
        }

        return list;
    }

    public static ModDetails? ParseDetails(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("mod", out var mod))
        {
            return null;
        }

        var html = mod.TryGetProperty("text", out var textEl) ? textEl.GetString() : null;
        var logo = FirstNonEmpty(
            mod.TryGetProperty("logofilename", out var lf) ? lf.GetString() : null,
            mod.TryGetProperty("logofile", out var lo) ? lo.GetString() : null,
            mod.TryGetProperty("logofiledb", out var ldb) ? ldb.GetString() : null);

        return new ModDetails
        {
            ModId = mod.TryGetProperty("modid", out var id) ? id.GetInt32() : 0,
            AssetId = mod.TryGetProperty("assetid", out var asset) ? asset.GetInt32() : 0,
            Name = mod.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "Unknown" : "Unknown",
            Author = mod.TryGetProperty("author", out var author) ? author.GetString() : null,
            Summary = mod.TryGetProperty("summary", out var summary) ? summary.GetString() : null,
            DescriptionText = StripHtml(html),
            Side = mod.TryGetProperty("side", out var side) ? side.GetString() : null,
            LogoUrl = logo,
            Downloads = mod.TryGetProperty("downloads", out var downloads) ? downloads.GetInt32() : 0,
            Follows = mod.TryGetProperty("follows", out var follows) ? follows.GetInt32() : 0,
            HomepageUrl = mod.TryGetProperty("homepageurl", out var home) ? home.GetString() : null,
            WikiUrl = mod.TryGetProperty("wikiurl", out var wiki) ? wiki.GetString() : null,
            SourceCodeUrl = mod.TryGetProperty("sourcecodeurl", out var source) ? source.GetString() : null,
            TrailerVideoUrl = mod.TryGetProperty("trailervideourl", out var trailer) ? trailer.GetString() : null,
            Tags = ReadStringArray(mod, "tags"),
            Screenshots = ParseScreenshots(mod),
            Releases = ParseReleases(mod),
        };
    }

    private static IReadOnlyList<ModReleaseInfo> ParseReleases(JsonElement mod)
    {
        var releases = new List<ModReleaseInfo>();
        if (!mod.TryGetProperty("releases", out var releasesEl) || releasesEl.ValueKind != JsonValueKind.Array)
        {
            return releases;
        }

        foreach (var release in releasesEl.EnumerateArray())
        {
            var fileId = release.TryGetProperty("fileid", out var fid) ? fid.GetInt32() : 0;
            if (fileId <= 0)
            {
                continue;
            }

            releases.Add(new ModReleaseInfo
            {
                FileId = fileId,
                ModVersion = release.TryGetProperty("modversion", out var ver) ? ver.GetString() ?? string.Empty : string.Empty,
                FileName = release.TryGetProperty("filename", out var fn) ? fn.GetString() : null,
                CompatibleGameVersions = ReadStringArray(release, "tags"),
                DownloadUrl = release.TryGetProperty("mainfile", out var main) && !string.IsNullOrWhiteSpace(main.GetString())
                    ? main.GetString()!
                    : $"https://mods.vintagestory.at/download?fileid={fileId}",
            });
        }

        return releases;
    }

    private static IReadOnlyList<ModScreenshot> ParseScreenshots(JsonElement mod)
    {
        var screenshots = new List<ModScreenshot>();
        if (!mod.TryGetProperty("screenshots", out var shotsEl) || shotsEl.ValueKind != JsonValueKind.Array)
        {
            return screenshots;
        }

        foreach (var shot in shotsEl.EnumerateArray())
        {
            screenshots.Add(new ModScreenshot
            {
                FileId = shot.TryGetProperty("fileid", out var sid) ? sid.GetInt32() : 0,
                MainUrl = shot.TryGetProperty("mainfile", out var main) ? main.GetString() : null,
                ThumbnailUrl = shot.TryGetProperty("thumbnailfilename", out var thumb) ? thumb.GetString() : null,
                FileName = shot.TryGetProperty("filename", out var name) ? name.GetString() : null,
            });
        }

        return screenshots;
    }

    private static ModSummary ParseSummary(JsonElement mod)
    {
        return new ModSummary
        {
            ModId = mod.TryGetProperty("modid", out var id) ? id.GetInt32() : 0,
            AssetId = mod.TryGetProperty("assetid", out var asset) ? asset.GetInt32() : 0,
            Name = mod.TryGetProperty("name", out var name) ? name.GetString() ?? "Unknown" : "Unknown",
            Author = mod.TryGetProperty("author", out var author) ? author.GetString() : null,
            Summary = mod.TryGetProperty("summary", out var summary) ? summary.GetString() : null,
            Downloads = mod.TryGetProperty("downloads", out var downloads) ? downloads.GetInt32() : 0,
            Follows = mod.TryGetProperty("follows", out var follows) ? follows.GetInt32() : 0,
            UrlAlias = mod.TryGetProperty("urlalias", out var alias) ? alias.GetString() : null,
            Side = mod.TryGetProperty("side", out var side) ? side.GetString() : null,
            LogoUrl = mod.TryGetProperty("logo", out var logo) ? logo.GetString() : null,
            Tags = ReadStringArray(mod, "tags"),
            LastReleased = mod.TryGetProperty("lastreleased", out var last) ? last.GetString() : null,
        };
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                list.Add(value);
            }
        }

        return list;
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var sb = new StringBuilder(html.Length);
        var inTag = false;
        foreach (var ch in html)
        {
            if (ch == '<')
            {
                inTag = true;
                continue;
            }

            if (ch == '>')
            {
                inTag = false;
                sb.Append(' ');
                continue;
            }

            if (!inTag)
            {
                sb.Append(ch);
            }
        }

        var text = WebUtility.HtmlDecode(sb.ToString());
        return CollapseWhitespace(text);
    }

    private static string CollapseWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var previousWasSpace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace)
                {
                    sb.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            previousWasSpace = false;
            sb.Append(ch);
        }

        return sb.ToString().Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string BuildSearchUrl(ModSearchQuery query)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            parts.Add("text=" + Uri.EscapeDataString(query.Text.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.OrderBy))
        {
            parts.Add("orderby=" + Uri.EscapeDataString(query.OrderBy));
        }

        if (!string.IsNullOrWhiteSpace(query.OrderDirection))
        {
            parts.Add("orderdirection=" + Uri.EscapeDataString(query.OrderDirection));
        }

        if (!string.IsNullOrWhiteSpace(query.GameVersion))
        {
            parts.Add("gv=" + Uri.EscapeDataString(query.GameVersion.Trim()));
        }

        return parts.Count == 0 ? "mods" : "mods?" + string.Join('&', parts);
    }

    private string CatalogCachePath => Path.Combine(_pathProvider.GetPaths().CacheDirectory, "mods", "catalog.json");

    private string DetailsCachePath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant())))[..20].ToLowerInvariant();
        return Path.Combine(_pathProvider.GetPaths().CacheDirectory, "mods", "details", hash + ".json");
    }

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private async Task<IReadOnlyList<ModSummary>?> ReadCatalogCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(CatalogCachePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(CatalogCachePath, cancellationToken).ConfigureAwait(false);
            var entry = JsonSerializer.Deserialize<CatalogCacheEntry>(json, CacheJsonOptions);
            if (entry is null || DateTimeOffset.UtcNow - entry.CachedAt > CatalogTtl)
            {
                return null;
            }

            return entry.Mods;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private async Task WriteCatalogCacheAsync(IReadOnlyList<ModSummary> mods, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogCachePath)!);
            var payload = new CatalogCacheEntry
            {
                CachedAt = DateTimeOffset.UtcNow,
                Mods = mods.ToList(),
            };
            var json = JsonSerializer.Serialize(payload, CacheJsonOptions);
            await File.WriteAllTextAsync(CatalogCachePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Could not write mod catalog cache");
        }
    }

    private async Task<ModDetails?> ReadDetailsCacheAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var path = DetailsCachePath(key);
            if (!File.Exists(path))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var entry = JsonSerializer.Deserialize<DetailsCacheEntry>(json, CacheJsonOptions);
            if (entry?.Mod is null || DateTimeOffset.UtcNow - entry.CachedAt > DetailsTtl)
            {
                return null;
            }

            return entry.Mod;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private async Task WriteDetailsCacheAsync(string key, ModDetails details, CancellationToken cancellationToken)
    {
        try
        {
            var path = DetailsCachePath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var payload = new DetailsCacheEntry
            {
                CachedAt = DateTimeOffset.UtcNow,
                Mod = details,
            };
            var json = JsonSerializer.Serialize(payload, CacheJsonOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Could not write mod details cache");
        }
    }

    private sealed class CatalogCacheEntry
    {
        public DateTimeOffset CachedAt { get; init; }
        public List<ModSummary> Mods { get; init; } = [];
    }

    private sealed class DetailsCacheEntry
    {
        public DateTimeOffset CachedAt { get; init; }
        public ModDetails? Mod { get; init; }
    }

    private static HttpClient CreateDefaultClient()
        => new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            BaseAddress = new Uri(ApiBase),
            Timeout = TimeSpan.FromSeconds(90),
        };
}
