using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.Servers;

public sealed partial class MasterServerClient
{
    private sealed class CatalogCacheEntry
    {
        public DateTimeOffset CachedAt { get; set; }
        public List<PublicServerSummary> Servers { get; set; } = [];
    }

    private sealed record CatalogCacheSnapshot(DateTimeOffset CachedAt, MasterServerCatalog Catalog);

    private string CatalogCachePath =>
        Path.Combine(_pathProvider.GetPaths().CacheDirectory, "servers", "catalog.json");

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
            if (entry?.Servers is null)
            {
                return null;
            }

            var catalog = new MasterServerCatalog
            {
                Servers = entry.Servers,
                FetchedAt = entry.CachedAt,
            };
            return new CatalogCacheSnapshot(entry.CachedAt, catalog);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogDebug(ex, "Could not read server catalog cache");
            return null;
        }
    }

    private async Task TryWriteCatalogCacheAsync(MasterServerCatalog catalog, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogCachePath)!);
            var payload = JsonSerializer.Serialize(new CatalogCacheEntry
            {
                CachedAt = catalog.FetchedAt,
                Servers = catalog.Servers.ToList(),
            }, CacheJsonOptions);
            await File.WriteAllTextAsync(CatalogCachePath, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Could not write server catalog cache");
        }
    }
}
