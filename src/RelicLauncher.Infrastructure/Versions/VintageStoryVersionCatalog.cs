using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Versions;
using RelicLauncher.Infrastructure.Endpoints;

namespace RelicLauncher.Infrastructure.Versions;

public sealed class VintageStoryVersionCatalog : IGameVersionCatalog
{
    private static readonly TimeSpan CatalogTtl = TimeSpan.FromHours(6);
    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly IAppPathProvider _pathProvider;
    private readonly IEndpointProvider _endpoints;
    private readonly ILogger<VintageStoryVersionCatalog> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<GameVersionInfo>? _memory;
    private DateTimeOffset _memoryAt;
    private string? _latestStableMemory;
    private DateTimeOffset _latestStableAt;

    public bool LastCatalogWasStale { get; private set; }

    public VintageStoryVersionCatalog(IAppPathProvider pathProvider, IEndpointProvider endpoints, ILogger<VintageStoryVersionCatalog> logger)
        : this(pathProvider, endpoints, logger, CreateDefaultClient())
    {
    }

    internal VintageStoryVersionCatalog(IAppPathProvider pathProvider, ILogger<VintageStoryVersionCatalog> logger, HttpClient httpClient)
        : this(pathProvider, new EndpointProvider(), logger, httpClient)
    {
    }

    internal VintageStoryVersionCatalog(IAppPathProvider pathProvider, IEndpointProvider endpoints, ILogger<VintageStoryVersionCatalog> logger, HttpClient httpClient)
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

    public async Task<Result<IReadOnlyList<GameVersionInfo>>> GetVersionsAsync(
        GameVersionChannel? channel = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var versions = await EnsureCatalogAsync(cancellationToken).ConfigureAwait(false);
            if (versions is null)
            {
                return Result<IReadOnlyList<GameVersionInfo>>.Failure("Could not load version catalog.");
            }

            if (channel is not null)
            {
                versions = versions.Where(v => v.Channel == channel).ToList();
            }

            return Result<IReadOnlyList<GameVersionInfo>>.Success(versions);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            _logger.LogWarning(ex, "Failed to fetch version catalog");
            return Result<IReadOnlyList<GameVersionInfo>>.Failure(ex.Message);
        }
    }

    public async Task<Result<string?>> GetLatestStableVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_latestStableMemory is not null && DateTimeOffset.UtcNow - _latestStableAt < CatalogTtl)
            {
                return Result<string?>.Success(_latestStableMemory);
            }

            var cached = await ReadLatestCacheAsync(cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                _latestStableMemory = cached;
                _latestStableAt = DateTimeOffset.UtcNow;
                _ = RefreshLatestInBackgroundAsync();
                return Result<string?>.Success(cached);
            }

            var text = await _httpClient.GetStringAsync(_endpoints.LatestStableUrl, cancellationToken).ConfigureAwait(false);
            var version = text.Trim();
            _latestStableMemory = string.IsNullOrWhiteSpace(version) ? null : version;
            _latestStableAt = DateTimeOffset.UtcNow;
            if (_latestStableMemory is not null)
            {
                await WriteLatestCacheAsync(_latestStableMemory, cancellationToken).ConfigureAwait(false);
            }

            return Result<string?>.Success(_latestStableMemory);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return Result<string?>.Failure(ex.Message);
        }
    }

    private async Task<IReadOnlyList<GameVersionInfo>?> EnsureCatalogAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_memory is not null && DateTimeOffset.UtcNow - _memoryAt < CatalogTtl)
            {
                return _memory;
            }

            var disk = await ReadCatalogCacheAsync(cancellationToken).ConfigureAwait(false);
            if (disk is not null)
            {
                _memory = disk;
                _memoryAt = DateTimeOffset.UtcNow;
                _ = RefreshCatalogInBackgroundAsync();
                return disk;
            }

            return await RefreshCatalogCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RefreshCatalogInBackgroundAsync()
    {
        try
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await RefreshCatalogCoreAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Background version catalog refresh failed");
        }
    }

    private async Task RefreshLatestInBackgroundAsync()
    {
        try
        {
            var text = await _httpClient.GetStringAsync(_endpoints.LatestStableUrl).ConfigureAwait(false);
            var version = text.Trim();
            if (string.IsNullOrWhiteSpace(version))
            {
                return;
            }

            _latestStableMemory = version;
            _latestStableAt = DateTimeOffset.UtcNow;
            await WriteLatestCacheAsync(version, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Background latest-stable refresh failed");
        }
    }

    private async Task<IReadOnlyList<GameVersionInfo>?> RefreshCatalogCoreAsync(CancellationToken cancellationToken)
    {
        var json = await _httpClient.GetStringAsync(_endpoints.VersionCatalogUrl, cancellationToken).ConfigureAwait(false);
        var versions = ParseCatalog(json);
        _memory = versions;
        _memoryAt = DateTimeOffset.UtcNow;
        await WriteCatalogCacheAsync(json, cancellationToken).ConfigureAwait(false);
        return versions;
    }

    public static IReadOnlyList<GameVersionInfo> ParseCatalog(string json)
    {
        using var document = JsonDocument.Parse(json);
        var list = new List<GameVersionInfo>();
        foreach (var versionProperty in document.RootElement.EnumerateObject())
        {
            var parsed = ParseVersionEntry(versionProperty);
            if (parsed is not null)
            {
                list.Add(parsed);
            }
        }

        return list
            .OrderByDescending(v => v.Version, Comparer<string>.Create(GameVersionComparer.Compare))
            .ToList();
    }

    private static GameVersionInfo? ParseVersionEntry(JsonProperty versionProperty)
    {
        var version = versionProperty.Name;
        var packages = new List<GameVersionPackage>();
        var isLatest = false;
        var channel = DetectChannel(version);

        foreach (var platformProperty in versionProperty.Value.EnumerateObject())
        {
            var package = ParsePackage(platformProperty, ref isLatest);
            if (package is not null)
            {
                packages.Add(package);
            }
        }

        if (packages.Count == 0)
        {
            return null;
        }

        return new GameVersionInfo
        {
            Version = version,
            Channel = channel,
            Packages = packages,
            IsLatest = isLatest,
        };
    }

    private static GameVersionChannel DetectChannel(string version)
    {
        return version.Contains('-', StringComparison.Ordinal) ||
               version.Contains("rc", StringComparison.OrdinalIgnoreCase) ||
               version.Contains("pre", StringComparison.OrdinalIgnoreCase)
            ? GameVersionChannel.Unstable
            : GameVersionChannel.Stable;
    }

    private static GameVersionPackage? ParsePackage(JsonProperty platformProperty, ref bool isLatest)
    {
        var platformKey = platformProperty.Name;
        if (platformKey is "linuxserver" or "windowsserver" or "windowsupdate")
        {
            return null;
        }

        var pkg = platformProperty.Value;
        if (!pkg.TryGetProperty("filename", out var fileNameEl) ||
            !pkg.TryGetProperty("urls", out var urlsEl))
        {
            return null;
        }

        var fileName = fileNameEl.GetString() ?? string.Empty;
        var cdn = urlsEl.TryGetProperty("cdn", out var cdnEl) ? cdnEl.GetString() : null;
        var local = urlsEl.TryGetProperty("local", out var localEl) ? localEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(cdn) && string.IsNullOrWhiteSpace(local))
        {
            return null;
        }

        if (pkg.TryGetProperty("latest", out var latestEl) && latestEl.ValueKind == JsonValueKind.Number && latestEl.GetInt32() == 1)
        {
            isLatest = true;
        }

        return new GameVersionPackage
        {
            PlatformKey = platformKey,
            FileName = fileName,
            CdnUrl = cdn ?? local!,
            LocalUrl = local,
            Md5 = pkg.TryGetProperty("md5", out var md5El) ? md5El.GetString() : null,
            FileSizeLabel = pkg.TryGetProperty("filesize", out var sizeEl) ? sizeEl.GetString() : null,
            Kind = DetectKind(fileName),
        };
    }

    private static ClientPackageKind DetectKind(string fileName)
    {
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            return ClientPackageKind.TarGz;
        }

        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return ClientPackageKind.Zip;
        }

        return ClientPackageKind.WindowsInstaller;
    }

    private string CatalogCachePath => Path.Combine(_pathProvider.GetPaths().CacheDirectory, "versions", "catalog.json");
    private string LatestCachePath => Path.Combine(_pathProvider.GetPaths().CacheDirectory, "versions", "lateststable.txt");

    private async Task<IReadOnlyList<GameVersionInfo>?> ReadCatalogCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(CatalogCachePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(CatalogCachePath, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("cachedAt", out var atEl) ||
                !DateTimeOffset.TryParse(atEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var cachedAt) ||
                DateTimeOffset.UtcNow - cachedAt > CatalogTtl ||
                !doc.RootElement.TryGetProperty("payload", out var payloadEl))
            {
                return null;
            }

            return ParseCatalog(payloadEl.GetString() ?? string.Empty);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private async Task WriteCatalogCacheAsync(string payloadJson, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogCachePath)!);
            var wrapper = JsonSerializer.Serialize(new
            {
                cachedAt = DateTimeOffset.UtcNow,
                payload = payloadJson,
            }, CacheJsonOptions);
            await File.WriteAllTextAsync(CatalogCachePath, wrapper, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Could not write version catalog cache");
        }
    }

    private async Task<string?> ReadLatestCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(LatestCachePath))
            {
                return null;
            }

            var info = new FileInfo(LatestCachePath);
            if (DateTimeOffset.UtcNow - new DateTimeOffset(info.LastWriteTimeUtc) > CatalogTtl)
            {
                return null;
            }

            var text = (await File.ReadAllTextAsync(LatestCachePath, cancellationToken).ConfigureAwait(false)).Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task WriteLatestCacheAsync(string version, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LatestCachePath)!);
            await File.WriteAllTextAsync(LatestCachePath, version, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Could not write latest-stable cache");
        }
    }

    private static HttpClient CreateDefaultClient()
        => new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(45),
        };
}
