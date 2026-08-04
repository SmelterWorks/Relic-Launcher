using System.Collections.Concurrent;
using Avalonia.Media.Imaging;

namespace RelicLauncher.App.Services;

public sealed class RemoteNewsImageLoader : IRemoteNewsImageLoader, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, Bitmap> _memoryCache = new(StringComparer.OrdinalIgnoreCase);

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
            return cached;
        }

        try
        {
            using var response = await _httpClient.GetAsync(normalized, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            _memoryCache[normalized] = bitmap;
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

    public void Dispose() => _httpClient.Dispose();

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
