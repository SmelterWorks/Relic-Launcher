using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;

namespace RelicLauncher.Infrastructure.Caching;

public sealed class DiskRemoteImageCache : IRemoteImageCache, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDir;
    private readonly ConcurrentDictionary<string, byte[]> _memory = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<DiskRemoteImageCache> _logger;

    public DiskRemoteImageCache(IAppPathProvider pathProvider, ILogger<DiskRemoteImageCache> logger)
    {
        _logger = logger;
        _cacheDir = Path.Combine(pathProvider.GetPaths().CacheDirectory, "images");
        Directory.CreateDirectory(_cacheDir);
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RelicLauncher/0.1.0");
    }

    public async Task<byte[]?> GetImageBytesAsync(string url, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeUrl(url);
        if (normalized is null)
        {
            return null;
        }

        if (_memory.TryGetValue(normalized, out var cached))
        {
            return cached;
        }

        var path = GetPath(normalized);
        try
        {
            if (File.Exists(path))
            {
                var disk = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                _memory[normalized] = disk;
                return disk;
            }

            using var response = await _httpClient.GetAsync(normalized, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
            _memory[normalized] = bytes;
            return bytes;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "Image cache fetch failed for {Url}", normalized);
            return null;
        }
    }

    public void Dispose() => _httpClient.Dispose();

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
