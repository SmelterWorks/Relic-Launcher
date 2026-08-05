using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;

namespace RelicLauncher.Infrastructure.Caching;

public sealed class DiskRemoteImageCache : IRemoteImageCache, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDir;
    private readonly ILogger<DiskRemoteImageCache> _logger;

    public DiskRemoteImageCache(IAppPathProvider pathProvider, ILogger<DiskRemoteImageCache> logger)
        : this(pathProvider, logger, CreateHttpClient())
    {
    }

    internal DiskRemoteImageCache(
        IAppPathProvider pathProvider,
        ILogger<DiskRemoteImageCache> logger,
        HttpClient httpClient)
    {
        _logger = logger;
        _cacheDir = Path.Combine(pathProvider.GetPaths().CacheDirectory, "images");
        Directory.CreateDirectory(_cacheDir);
        _httpClient = httpClient;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RelicLauncher/0.1.0");
        return client;
    }

    public async Task<byte[]?> GetImageBytesAsync(string url, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeUrl(url);
        if (normalized is null)
        {
            return null;
        }

        try
        {
            var fromDisk = await TryReadDiskAsync(normalized, cancellationToken).ConfigureAwait(false);
            if (fromDisk is not null)
            {
                return fromDisk;
            }

            return await FetchAndStoreAsync(normalized, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "Image cache fetch failed for {Url}", normalized);
            return null;
        }
    }

    public void Dispose() => _httpClient.Dispose();

    private async Task<byte[]?> TryReadDiskAsync(string normalized, CancellationToken cancellationToken)
    {
        var path = GetPath(normalized);
        if (!File.Exists(path))
        {
            return null;
        }

        var disk = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (disk.Length > RelicDefaults.MaxRemoteImageBytes)
        {
            return null;
        }

        return disk;
    }

    private async Task<byte[]?> FetchAndStoreAsync(string normalized, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(normalized, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var length = response.Content.Headers.ContentLength;
        if (length is > 0 && length.Value > RelicDefaults.MaxRemoteImageBytes)
        {
            return null;
        }

        using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > RelicDefaults.MaxRemoteImageBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        var bytes = buffer.ToArray();
        await File.WriteAllBytesAsync(GetPath(normalized), bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    private string GetPath(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..24].ToLowerInvariant();
        var ext = ".img";
        if (url.Contains(".png", StringComparison.OrdinalIgnoreCase))
        {
            ext = ".png";
        }
        else if (url.Contains(".jpg", StringComparison.OrdinalIgnoreCase) || url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            ext = ".jpg";
        }
        else if (url.Contains(".webp", StringComparison.OrdinalIgnoreCase))
        {
            ext = ".webp";
        }

        return Path.Combine(_cacheDir, hash + ext);
    }

    internal static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + url;
        }

        return url;
    }
}
