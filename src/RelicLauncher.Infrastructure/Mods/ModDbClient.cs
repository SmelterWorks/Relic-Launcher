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

public sealed partial class ModDbClient : IModDbClient
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
    private string? _filteredCacheKey;
    private List<ModSummary>? _filteredCache;
    private IReadOnlyList<ModSummary>? _filteredCacheSource;

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

    public async Task<Result<IReadOnlyList<ModSummary>>> GetCatalogAsync(
        bool preferCache = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var catalog = await EnsureCatalogAsync(forceRefresh: !preferCache, cancellationToken).ConfigureAwait(false);
            return catalog is null
                ? Result<IReadOnlyList<ModSummary>>.Failure("Could not load mod catalog.")
                : Result<IReadOnlyList<ModSummary>>.Success(catalog);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            _logger.LogWarning(ex, "ModDB catalog fetch failed");
            return Result<IReadOnlyList<ModSummary>>.Failure(ex.Message);
        }
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

            var filtered = GetFilteredMods(resolved.Value!.Mods, query);
            var offset = (page - 1) * pageSize;
            var pageItems = offset >= filtered.Count
                ? []
                : filtered.GetRange(offset, Math.Min(pageSize, filtered.Count - offset));
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
        return catalog is null ? null : GetFilteredMods(catalog, query);
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
                    InvalidateFilterCache();
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
            InvalidateFilterCache();
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
            InvalidateFilterCache();
            return stale.Mods;
        }

        return _memoryCatalog;
    }

    private static bool HasModsProperty(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("mods", out _);
    }

    private List<ModSummary> GetFilteredMods(IReadOnlyList<ModSummary> source, ModSearchQuery query)
    {
        var key = BuildFilterCacheKey(query);
        if (_filteredCache is not null &&
            ReferenceEquals(_filteredCacheSource, source) &&
            string.Equals(_filteredCacheKey, key, StringComparison.Ordinal))
        {
            return _filteredCache;
        }

        var filtered = FilterAndSort(source, query);
        _filteredCacheKey = key;
        _filteredCacheSource = source;
        _filteredCache = filtered;
        return filtered;
    }

    private void InvalidateFilterCache()
    {
        _filteredCacheKey = null;
        _filteredCache = null;
        _filteredCacheSource = null;
    }

    private static string BuildFilterCacheKey(ModSearchQuery query)
    {
        var tags = query.TagNames is { Count: > 0 }
            ? string.Join('\n', query.TagNames.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var tagIds = query.TagIds is { Count: > 0 }
            ? string.Join('\n', query.TagIds.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        return string.Join(
            '\u001f',
            query.Text ?? string.Empty,
            query.Side ?? string.Empty,
            query.OrderBy ?? string.Empty,
            query.OrderDirection ?? string.Empty,
            query.GameVersion ?? string.Empty,
            tags,
            tagIds);
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
            var requiredTags = query.TagNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(m => requiredTags.All(required => m.Tags.Contains(required, StringComparer.OrdinalIgnoreCase)));
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
    private string ApiUrl(string relative)
        => new Uri(new Uri(_endpoints.ModDbApiBaseUrl), relative).ToString();

    private static HttpClient CreateDefaultClient()
        => new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(90),
        };
}
