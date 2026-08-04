using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Endpoints;

namespace RelicLauncher.Infrastructure.Mods;

public sealed class ModDbClient : IModDbClient
{
    private static readonly TimeSpan CatalogTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan DetailsTtl = TimeSpan.FromHours(12);
    private readonly HttpClient _httpClient;
    private readonly IAppPathProvider _pathProvider;
    private readonly IEndpointProvider _endpoints;
    private readonly ILogger<ModDbClient> _logger;
    private readonly SemaphoreSlim _catalogGate = new(1, 1);
    private IReadOnlyList<ModSummary>? _memoryCatalog;
    private DateTimeOffset _memoryCatalogAt;
    private bool _lastCatalogWasStale;

    public ModDbClient(IAppPathProvider pathProvider, IEndpointProvider endpoints, ILogger<ModDbClient> logger)
        : this(pathProvider, endpoints, logger, CreateDefaultClient())
    {
    }

    internal ModDbClient(IAppPathProvider pathProvider, ILogger<ModDbClient> logger, HttpClient httpClient)
        : this(pathProvider, new EndpointProvider(), logger, httpClient)
    {
    }

    internal ModDbClient(IAppPathProvider pathProvider, IEndpointProvider endpoints, ILogger<ModDbClient> logger, HttpClient httpClient)
    {
        _pathProvider = pathProvider;
        _endpoints = endpoints;
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

    public async Task<Result<IReadOnlyList<ModTagInfo>>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cached = await TryReadTagsCacheAsync(cancellationToken).ConfigureAwait(false);
            if (cached is not null && DateTimeOffset.UtcNow - cached.CachedAt < CatalogTtl)
            {
                return Result<IReadOnlyList<ModTagInfo>>.Success(cached.Tags);
            }

            var url = _endpoints.ModDbApiBaseUrl.TrimEnd('/') + "/tags";
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                if (cached?.Tags is { Count: > 0 })
                {
                    return Result<IReadOnlyList<ModTagInfo>>.Success(cached.Tags);
                }

                return Result<IReadOnlyList<ModTagInfo>>.Failure(
                    $"Could not load ModDB tags ({(int)response.StatusCode}).");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var tags = ParseTags(json);
            await TryWriteTagsCacheAsync(tags, cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<ModTagInfo>>.Success(tags);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            _logger.LogWarning(ex, "ModDB tags fetch failed");
            return Result<IReadOnlyList<ModTagInfo>>.Failure(ex.Message);
        }
    }

    public static IReadOnlyList<ModTagInfo> ParseTags(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("tags", out var tagsEl) || tagsEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<ModTagInfo>();
        foreach (var tag in tagsEl.EnumerateArray())
        {
            var id = tag.TryGetProperty("tagid", out var idEl)
                ? idEl.ValueKind == JsonValueKind.Number
                    ? idEl.GetInt32().ToString(CultureInfo.InvariantCulture)
                    : idEl.GetString()
                : null;
            var name = tag.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            list.Add(new ModTagInfo
            {
                TagId = id,
                Name = name,
                Color = tag.TryGetProperty("color", out var color) ? color.GetString() : null,
            });
        }

        return list
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Result<ModSearchResult>> SearchAsync(ModSearchQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize <= 0 ? RelicDefaults.ModBrowsePageSize : query.PageSize, 1, 100);
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
                IsStale = resolved.Value.IsStale,
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
        if (!string.IsNullOrWhiteSpace(query.GameVersion) ||
            !string.IsNullOrWhiteSpace(query.Text) ||
            query.TagIds.Count > 0)
        {
            var remote = await FetchRemoteSearchAsync(query, cancellationToken).ConfigureAwait(false);
            if (remote.IsSuccess)
            {
                return Result<SearchSource>.Success(new SearchSource(remote.Value!, false, false));
            }

            var fallback = await TryLocalFilterAsync(query, cancellationToken).ConfigureAwait(false);
            return fallback is null
                ? Result<SearchSource>.Failure(remote.Error ?? "Mod search failed.")
                : Result<SearchSource>.Success(new SearchSource(fallback, true, _lastCatalogWasStale));
        }

        var catalog = await EnsureCatalogAsync(forceRefresh: !query.PreferCache, cancellationToken).ConfigureAwait(false);
        return catalog is null
            ? Result<SearchSource>.Failure("Could not load mod catalog.")
            : Result<SearchSource>.Success(new SearchSource(catalog, true, _lastCatalogWasStale));
    }

    private sealed record SearchSource(IReadOnlyList<ModSummary> Mods, bool FromCache, bool IsStale);

    public async Task<Result<ModDetails>> GetModAsync(string modIdOrAlias, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modIdOrAlias))
        {
            return Result<ModDetails>.Failure("Mod id is required.");
        }

        var key = modIdOrAlias.Trim();
        var cached = await TryReadDetailsCacheAsync(key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            if (DateTimeOffset.UtcNow - cached.CachedAt <= DetailsTtl)
            {
                return Result<ModDetails>.Success(cached.Mod);
            }

            _ = RefreshDetailsInBackgroundAsync(key);
            return Result<ModDetails>.Success(cached.Mod);
        }

        try
        {
            var json = await _httpClient.GetStringAsync(ApiUrl($"mod/{Uri.EscapeDataString(key)}"), cancellationToken)
                .ConfigureAwait(false);
            var details = ParseDetails(json, _endpoints.BuildModDownloadUrl);
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
            var stale = await TryReadDetailsCacheAsync(key, cancellationToken).ConfigureAwait(false);
            if (stale is not null)
            {
                return Result<ModDetails>.Success(stale.Mod);
            }

            return Result<ModDetails>.Failure(ex.Message);
        }
    }

    private async Task RefreshDetailsInBackgroundAsync(string key)
    {
        try
        {
            var json = await _httpClient.GetStringAsync(ApiUrl($"mod/{Uri.EscapeDataString(key)}"), CancellationToken.None)
                .ConfigureAwait(false);
            var details = ParseDetails(json, _endpoints.BuildModDownloadUrl);
            if (details is not null)
            {
                await WriteDetailsCacheAsync(key, details, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Background mod details refresh failed for {Mod}", key);
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
            var url = ApiUrl(BuildSearchUrl(query));
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
                _lastCatalogWasStale = false;
                return _memoryCatalog;
            }

            if (!forceRefresh)
            {
                var disk = await TryReadCatalogCacheAsync(cancellationToken).ConfigureAwait(false);
                if (disk is not null)
                {
                    _lastCatalogWasStale = DateTimeOffset.UtcNow - disk.CachedAt > CatalogTtl;
                    _memoryCatalog = disk.Mods;
                    _memoryCatalogAt = DateTimeOffset.UtcNow;
                    _ = RefreshCatalogInBackgroundAsync();
                    return disk.Mods;
                }
            }

            _lastCatalogWasStale = false;
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
            var json = await _httpClient.GetStringAsync(ApiUrl("mods?orderby=downloads"), cancellationToken).ConfigureAwait(false);
            var mods = ParseSearch(json);
            if (!HasModsProperty(json))
            {
                throw new JsonException("Mod catalog response is missing a mods array.");
            }

            _lastCatalogWasStale = false;
            _memoryCatalog = mods;
            _memoryCatalogAt = DateTimeOffset.UtcNow;
            await WriteCatalogCacheAsync(mods, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Cached ModDB catalog with {Count} mods", mods.Count);
            return mods;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            _logger.LogWarning(ex, "Failed to refresh ModDB catalog");
            return await TryServeStaleCatalogAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<ModSummary>?> TryServeStaleCatalogAsync(CancellationToken cancellationToken)
    {
        var stale = await TryReadCatalogCacheAsync(cancellationToken).ConfigureAwait(false);
        if (stale is not null)
        {
            _lastCatalogWasStale = true;
            _memoryCatalog = stale.Mods;
            _memoryCatalogAt = DateTimeOffset.UtcNow;
            return stale.Mods;
        }

        return _memoryCatalog;
    }

    private static bool HasModsProperty(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("mods", out _);
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

        if (!string.IsNullOrWhiteSpace(query.Side) &&
            !string.Equals(query.Side, "any", StringComparison.OrdinalIgnoreCase))
        {
            var side = query.Side.Trim();
            filtered = filtered.Where(m =>
                string.Equals(m.Side, side, StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(side, "client", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(m.Side, "both", StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(side, "server", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(m.Side, "both", StringComparison.OrdinalIgnoreCase)));
        }

        if (query.TagNames is { Count: > 0 })
        {
            filtered = filtered.Where(m =>
                query.TagNames.All(required =>
                    m.Tags.Any(t => string.Equals(t, required, StringComparison.OrdinalIgnoreCase))));
        }

        var orderBy = query.OrderBy ?? "downloads";
        var desc = !string.Equals(query.OrderDirection, "asc", StringComparison.OrdinalIgnoreCase);
        filtered = orderBy.ToLowerInvariant() switch
        {
            "name" => desc
                ? filtered.OrderByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
            "follows" => desc ? filtered.OrderByDescending(m => m.Follows) : filtered.OrderBy(m => m.Follows),
            "trending" or "trendingpoints" => desc
                ? filtered.OrderByDescending(m => m.TrendingPoints)
                : filtered.OrderBy(m => m.TrendingPoints),
            "updated" or "lastreleased" => desc
                ? filtered.OrderByDescending(m => ParseReleaseDate(m.LastReleased))
                : filtered.OrderBy(m => ParseReleaseDate(m.LastReleased)),
            _ => desc ? filtered.OrderByDescending(m => m.Downloads) : filtered.OrderBy(m => m.Downloads),
        };

        return filtered.ToList();
    }

    private static DateTime ParseReleaseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.MinValue;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTime.MinValue;
    }

    public static IReadOnlyList<ModSummary> ParseSearch(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("mods", out var modsEl))
        {
            return [];
        }

        if (modsEl.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Mod catalog mods field must be a JSON array.");
        }

        var list = new List<ModSummary>();
        foreach (var mod in modsEl.EnumerateArray())
        {
            list.Add(ParseSummary(mod));
        }

        return list;
    }

    public static ModDetails? ParseDetails(string json, Func<int, string>? buildDownloadUrl = null)
    {
        buildDownloadUrl ??= VintageStoryEndpoints.BuildModDownloadUrl;
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
            UrlAlias = mod.TryGetProperty("urlalias", out var alias) ? alias.GetString() : null,
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
            Releases = ParseReleases(mod, buildDownloadUrl),
        };
    }

    private static IReadOnlyList<ModReleaseInfo> ParseReleases(JsonElement mod, Func<int, string> buildDownloadUrl)
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
                    : buildDownloadUrl(fileId),
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
            TrendingPoints = mod.TryGetProperty("trendingpoints", out var trending) ? trending.GetInt32() : 0,
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

        var normalized = html
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</div>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</h1>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</h2>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</h3>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</tr>", "\n", StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder(normalized.Length);
        var inTag = false;
        foreach (var ch in normalized)
        {
            if (ch == '<')
            {
                inTag = true;
                continue;
            }

            if (ch == '>')
            {
                inTag = false;
                continue;
            }

            if (!inTag)
            {
                sb.Append(ch);
            }
        }

        var text = WebUtility.HtmlDecode(sb.ToString());
        return NormalizeDescriptionWhitespace(text);
    }

    private static string NormalizeDescriptionWhitespace(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var cleaned = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            cleaned.Add(CollapseHorizontalWhitespace(line).TrimEnd());
        }

        var sb = new StringBuilder(text.Length);
        var blankRun = 0;
        foreach (var line in cleaned)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                blankRun++;
                if (blankRun <= 2)
                {
                    sb.Append('\n');
                }

                continue;
            }

            blankRun = 0;
            if (sb.Length > 0 && sb[^1] != '\n')
            {
                sb.Append('\n');
            }

            sb.Append(line);
            sb.Append('\n');
        }

        return sb.ToString().Trim();
    }

    private static string CollapseHorizontalWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var previousWasSpace = false;
        foreach (var ch in text)
        {
            if (ch is ' ' or '\t')
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

        return sb.ToString();
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

        foreach (var tagId in query.TagIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            parts.Add("tagids[]=" + Uri.EscapeDataString(tagId.Trim()));
        }

        return parts.Count == 0 ? "mods" : "mods?" + string.Join('&', parts);
    }

    private string CatalogCachePath => Path.Combine(_pathProvider.GetPaths().CacheDirectory, "mods", "catalog.json");

    private string TagsCachePath => Path.Combine(_pathProvider.GetPaths().CacheDirectory, "mods", "tags.json");

    private string DetailsCachePath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant())))[..20].ToLowerInvariant();
        return Path.Combine(_pathProvider.GetPaths().CacheDirectory, "mods", "details", hash + ".json");
    }

    private async Task<TagsCacheSnapshot?> TryReadTagsCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(TagsCachePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(TagsCachePath, cancellationToken).ConfigureAwait(false);
            var entry = JsonSerializer.Deserialize<TagsCacheEntry>(json, CacheJsonOptions);
            if (entry?.Tags is null)
            {
                return null;
            }

            return new TagsCacheSnapshot(entry.CachedAt, entry.Tags);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private async Task TryWriteTagsCacheAsync(IReadOnlyList<ModTagInfo> tags, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TagsCachePath)!);
            var payload = JsonSerializer.Serialize(new TagsCacheEntry
            {
                CachedAt = DateTimeOffset.UtcNow,
                Tags = tags.ToList(),
            }, CacheJsonOptions);
            await File.WriteAllTextAsync(TagsCachePath, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Could not write ModDB tags cache");
        }
    }

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private async Task<CatalogCacheSnapshot?> TryReadCatalogCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(CatalogCachePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(CatalogCachePath, cancellationToken).ConfigureAwait(false);
            var entry = JsonSerializer.Deserialize<CatalogCacheEntry>(json, CacheJsonOptions);
            if (entry?.Mods is null)
            {
                return null;
            }

            return new CatalogCacheSnapshot(entry.CachedAt, entry.Mods);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private async Task<DetailsCacheSnapshot?> TryReadDetailsCacheAsync(string key, CancellationToken cancellationToken)
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
            if (entry?.Mod is null)
            {
                return null;
            }

            return new DetailsCacheSnapshot(entry.CachedAt, entry.Mod);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private sealed record CatalogCacheSnapshot(DateTimeOffset CachedAt, IReadOnlyList<ModSummary> Mods);

    private sealed record DetailsCacheSnapshot(DateTimeOffset CachedAt, ModDetails Mod);

    private sealed record TagsCacheSnapshot(DateTimeOffset CachedAt, IReadOnlyList<ModTagInfo> Tags);

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

    private sealed class TagsCacheEntry
    {
        public DateTimeOffset CachedAt { get; set; }
        public List<ModTagInfo>? Tags { get; set; }
    }

    private string ApiUrl(string relative)
        => new Uri(new Uri(_endpoints.ModDbApiBaseUrl), relative).ToString();

    private static HttpClient CreateDefaultClient()
        => new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(90),
        };
}
