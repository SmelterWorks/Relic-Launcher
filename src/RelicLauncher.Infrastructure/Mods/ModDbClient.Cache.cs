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

public sealed partial class ModDbClient
{
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

}
