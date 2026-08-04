using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using RelicLauncher.Core.Constants;

namespace RelicLauncher.App.Services;

public sealed class RemoteNewsImageLoader : IRemoteNewsImageLoader, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, Bitmap> _memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _lru = new();
    private readonly Lock _lruGate = new();

    public RemoteNewsImageLoader()
    {
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RelicLauncher/0.1.0");
    }

    public async Task<Bitmap?> LoadAsync(string url, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeUrl(url);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (_memoryCache.TryGetValue(normalized, out var cached))
        {
            Touch(normalized);
            return cached;
        }

        try
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

            buffer.Position = 0;
            var bitmap = new Bitmap(buffer);
            Remember(normalized, bitmap);
            return bitmap;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var bitmap in _memoryCache.Values)
        {
            bitmap.Dispose();
        }

        _memoryCache.Clear();
        _httpClient.Dispose();
    }

    private void Remember(string key, Bitmap bitmap)
    {
        if (_memoryCache.TryAdd(key, bitmap))
        {
            Touch(key);
            EvictIfNeeded();
            return;
        }

        bitmap.Dispose();
        Touch(key);
    }

    private void Touch(string key)
    {
        lock (_lruGate)
        {
            _lru.Remove(key);
            _lru.AddFirst(key);
        }
    }

    private void EvictIfNeeded()
    {
        lock (_lruGate)
        {
            while (_lru.Count > RelicDefaults.RemoteImageMemoryCacheEntries)
            {
                var last = _lru.Last;
                if (last is null)
                {
                    break;
                }

                _lru.RemoveLast();
                if (_memoryCache.TryRemove(last.Value, out var bitmap))
                {
                    bitmap.Dispose();
                }
            }
        }
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
