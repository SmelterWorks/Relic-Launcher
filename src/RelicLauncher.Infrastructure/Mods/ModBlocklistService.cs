using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Mods;

public sealed class ModBlocklistService : IModBlocklistService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    private readonly HttpClient _httpClient;
    private readonly IAppPathProvider _pathProvider;
    private readonly IEndpointProvider _endpoints;
    private readonly ILogger<ModBlocklistService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<ModBlocklistEntry>? _memory;
    private DateTimeOffset _memoryAt;

    public ModBlocklistService(
        IAppPathProvider pathProvider,
        IEndpointProvider endpoints,
        ILogger<ModBlocklistService> logger)
        : this(pathProvider, endpoints, logger, CreateDefaultClient())
    {
    }

    internal ModBlocklistService(
        IAppPathProvider pathProvider,
        IEndpointProvider endpoints,
        ILogger<ModBlocklistService> logger,
        HttpClient httpClient)
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

    public async Task<Result<IReadOnlyList<ModBlocklistEntry>>> GetEntriesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_memory is not null && DateTimeOffset.UtcNow - _memoryAt < CacheTtl)
            {
                return Result<IReadOnlyList<ModBlocklistEntry>>.Success(_memory);
            }

            var disk = await TryReadCacheAsync(cancellationToken).ConfigureAwait(false);
            if (disk is not null && DateTimeOffset.UtcNow - disk.CachedAt < CacheTtl)
            {
                _memory = disk.Entries;
                _memoryAt = disk.CachedAt;
                return Result<IReadOnlyList<ModBlocklistEntry>>.Success(_memory);
            }

            var url = BuildBlocklistUrl();
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                if (disk?.Entries is { Count: > 0 })
                {
                    _memory = disk.Entries;
                    _memoryAt = disk.CachedAt;
                    return Result<IReadOnlyList<ModBlocklistEntry>>.Success(_memory);
                }

                return Result<IReadOnlyList<ModBlocklistEntry>>.Failure(
                    $"Could not load mod blocklist ({(int)response.StatusCode}).");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var entries = Parse(json);
            _memory = entries;
            _memoryAt = DateTimeOffset.UtcNow;
            await TryWriteCacheAsync(entries, _memoryAt, cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<ModBlocklistEntry>>.Success(entries);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            _logger.LogWarning(ex, "Mod blocklist fetch failed");
            return Result<IReadOnlyList<ModBlocklistEntry>>.Failure(ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Result<ModBlocklistEntry?>> FindMatchAsync(
        string? modId,
        string? modVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return Result<ModBlocklistEntry?>.Success(null);
        }

        var entries = await GetEntriesAsync(cancellationToken).ConfigureAwait(false);
        if (!entries.IsSuccess)
        {
            return Result<ModBlocklistEntry?>.Failure(entries.Error ?? "Blocklist unavailable.");
        }

        return Result<ModBlocklistEntry?>.Success(FindMatch(entries.Value!, modId, modVersion));
    }

    public static ModBlocklistEntry? FindMatch(
        IReadOnlyList<ModBlocklistEntry> entries,
        string modId,
        string? modVersion)
    {
        if (!string.IsNullOrWhiteSpace(modVersion))
        {
            var exact = $"{modId.Trim()}@{modVersion.Trim()}";
            var hit = entries.FirstOrDefault(e =>
                string.Equals(e.Id, exact, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                return hit;
            }
        }

        return entries.FirstOrDefault(e =>
            string.Equals(e.ModId, modId.Trim(), StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(modVersion) ||
             string.Equals(e.Version, modVersion.Trim(), StringComparison.OrdinalIgnoreCase) ||
             string.IsNullOrWhiteSpace(e.Version)));
    }

    public static IReadOnlyList<ModBlocklistEntry> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<ModBlocklistEntry>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            list.Add(new ModBlocklistEntry
            {
                Id = id,
                Reason = item.TryGetProperty("reason", out var reason) ? reason.GetString() : null,
            });
        }

        return list;
    }

    private string BuildBlocklistUrl()
    {
        var cdn = _endpoints.CdnBaseUrl.TrimEnd('/') + "/";
        return cdn + "api/blockedmods.json";
    }

    private string CachePath => Path.Combine(_pathProvider.GetPaths().CacheDirectory, "mods", "blockedmods.json");

    private async Task<BlocklistCacheSnapshot?> TryReadCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(CachePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(CachePath, cancellationToken).ConfigureAwait(false);
            var entry = JsonSerializer.Deserialize<BlocklistCacheEntry>(json, CacheJsonOptions);
            if (entry?.Entries is null)
            {
                return null;
            }

            return new BlocklistCacheSnapshot(entry.CachedAt, entry.Entries);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private async Task TryWriteCacheAsync(
        IReadOnlyList<ModBlocklistEntry> entries,
        DateTimeOffset cachedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            var payload = JsonSerializer.Serialize(new BlocklistCacheEntry
            {
                CachedAt = cachedAt,
                Entries = entries.ToList(),
            }, CacheJsonOptions);
            await File.WriteAllTextAsync(CachePath, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Could not write mod blocklist cache");
        }
    }

    private static HttpClient CreateDefaultClient()
        => new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private sealed class BlocklistCacheEntry
    {
        public DateTimeOffset CachedAt { get; set; }
        public List<ModBlocklistEntry>? Entries { get; set; }
    }

    private sealed record BlocklistCacheSnapshot(DateTimeOffset CachedAt, IReadOnlyList<ModBlocklistEntry> Entries);
}
