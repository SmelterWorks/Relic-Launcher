using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Endpoints;

namespace RelicLauncher.Infrastructure.Servers;

public sealed partial class MasterServerClient : IMasterServerClient
{
    private const int MaxResponseBytes = 8 * 1024 * 1024;
    private static readonly TimeSpan FreshTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StaleTtl = TimeSpan.FromHours(24);

    private readonly HttpClient _httpClient;
    private readonly IAppPathProvider _pathProvider;
    private readonly IEndpointProvider _endpoints;
    private readonly ILogger<MasterServerClient> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private MasterServerCatalog? _memoryCatalog;
    private DateTimeOffset _memoryCatalogAt;
    private bool _memoryFromCache;

    public MasterServerClient(
        IAppPathProvider pathProvider,
        IEndpointProvider endpoints,
        ILogger<MasterServerClient> logger)
        : this(pathProvider, endpoints, logger, CreateDefaultClient())
    {
    }

    internal MasterServerClient(
        IAppPathProvider pathProvider,
        IEndpointProvider endpoints,
        ILogger<MasterServerClient> logger,
        HttpClient httpClient)
    {
        _pathProvider = pathProvider;
        _endpoints = endpoints;
        _logger = logger;
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("RelicLauncher", BuildMetadata.Version));
        }
    }

    public async Task<Result<MasterServerFetchResult>> FetchCatalogAsync(
        bool preferCache = true,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (preferCache && _memoryCatalog is not null &&
                DateTimeOffset.UtcNow - _memoryCatalogAt < FreshTtl)
            {
                return Success(_memoryCatalog, _memoryFromCache, isStale: false, usedOfficial: false);
            }

            if (!preferCache || _memoryCatalog is null ||
                DateTimeOffset.UtcNow - _memoryCatalogAt >= FreshTtl)
            {
                var network = await TryFetchFromNetworkAsync(cancellationToken).ConfigureAwait(false);
                if (network.IsSuccess)
                {
                    StoreMemory(network.Value!);
                    await TryWriteCatalogCacheAsync(network.Value!.Catalog, cancellationToken).ConfigureAwait(false);
                    return network;
                }

                var disk = await TryReadCatalogCacheAsync(cancellationToken).ConfigureAwait(false);
                if (disk is not null && DateTimeOffset.UtcNow - disk.CachedAt < StaleTtl)
                {
                    _memoryCatalog = disk.Catalog;
                    _memoryCatalogAt = disk.CachedAt;
                    _memoryFromCache = true;
                    return Success(disk.Catalog, fromCache: true, isStale: true, usedOfficial: false);
                }

                if (network.Error is not null)
                {
                    return Result<MasterServerFetchResult>.Failure(network.Error);
                }
            }

            if (_memoryCatalog is not null)
            {
                return Success(_memoryCatalog, _memoryFromCache, isStale: true, usedOfficial: false);
            }

            return Result<MasterServerFetchResult>.Failure("Could not load the public server list.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Result<MasterServerFetchResult>> TryFetchFromNetworkAsync(CancellationToken cancellationToken)
    {
        var primary = await TryFetchUrlAsync(_endpoints.ServerListUrl, cancellationToken).ConfigureAwait(false);
        if (primary.IsSuccess)
        {
            _logger.LogDebug("Loaded public server list from SmelterWorks proxy.");
            return Success(primary.Value!, fromCache: false, isStale: false, usedOfficial: false);
        }

        var fallback = await TryFetchUrlAsync(VintageStoryEndpoints.MasterServerListUrl, cancellationToken)
            .ConfigureAwait(false);
        if (fallback.IsSuccess)
        {
            _logger.LogDebug("Loaded public server list from official masterserver fallback.");
            return Success(fallback.Value!, fromCache: false, isStale: false, usedOfficial: true);
        }

        return Result<MasterServerFetchResult>.Failure(
            "Could not reach the server list (SmelterWorks proxy or official catalog).");
    }

    private async Task<Result<MasterServerCatalog>> TryFetchUrlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme is not "https")
            {
                return Result<MasterServerCatalog>.Failure("Server list URL must use HTTPS.");
            }

            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<MasterServerCatalog>.Failure($"Server list request failed ({(int)response.StatusCode}).");
            }

            var bytes = await ReadLimitedContentAsync(response, cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                return Result<MasterServerCatalog>.Failure("Server list response was too large.");
            }

            var json = System.Text.Encoding.UTF8.GetString(bytes);
            var catalog = ParseCatalog(json);
            if (catalog is null)
            {
                return Result<MasterServerCatalog>.Failure("Server list response was not valid JSON.");
            }

            return Result<MasterServerCatalog>.Success(catalog);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogDebug(ex, "Server list fetch failed for {Url}", url);
            return Result<MasterServerCatalog>.Failure(ex.Message);
        }
    }

    private static async Task<byte[]?> ReadLimitedContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            var total = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > MaxResponseBytes)
                {
                    return null;
                }

                memory.Write(buffer, 0, read);
            }

            return memory.ToArray();
        }
    }

    private void StoreMemory(MasterServerFetchResult result)
    {
        _memoryCatalog = result.Catalog;
        _memoryCatalogAt = result.Catalog.FetchedAt;
        _memoryFromCache = result.FromCache;
    }

    private static Result<MasterServerFetchResult> Success(
        MasterServerCatalog catalog,
        bool fromCache,
        bool isStale,
        bool usedOfficial)
        => Result<MasterServerFetchResult>.Success(new MasterServerFetchResult
        {
            Catalog = catalog,
            FromCache = fromCache,
            IsStale = isStale,
            UsedOfficialFallback = usedOfficial,
        });

    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        return client;
    }
}
