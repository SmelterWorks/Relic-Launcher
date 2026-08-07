using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Updates;

public sealed class LauncherUpdateDownloader
{
    private readonly IAppPathProvider _pathProvider;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LauncherUpdateDownloader> _logger;

    public LauncherUpdateDownloader(
        IAppPathProvider pathProvider,
        ILogger<LauncherUpdateDownloader> logger)
        : this(pathProvider, logger, CreateHttpClient())
    {
    }

    internal LauncherUpdateDownloader(
        IAppPathProvider pathProvider,
        ILogger<LauncherUpdateDownloader> logger,
        HttpClient httpClient)
    {
        _pathProvider = pathProvider;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<Result<string>> DownloadVerifiedAsync(
        LauncherUpdateAsset asset,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!UpdateManifestParser.IsAllowedAssetUrl(asset.Url))
        {
            return Result<string>.Failure("Update asset URL is not allowed.");
        }

        var cacheDir = Path.Combine(_pathProvider.GetPaths().CacheDirectory, "launcher-updates");
        Directory.CreateDirectory(cacheDir);
        var targetPath = Path.Combine(cacheDir, asset.Filename);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.Url);
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Result<string>.Failure($"Download failed with status {(int)response.StatusCode}.");
            }

            var verify = await SaveAndVerifyAsync(
                response,
                asset,
                targetPath,
                progress,
                cancellationToken).ConfigureAwait(false);
            return verify;
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Launcher update download failed for {Url}", asset.Url);
            TryDelete(targetPath);
            return Result<string>.Failure(ex.Message);
        }
    }

    private static async Task<Result<string>> SaveAndVerifyAsync(
        HttpResponseMessage response,
        LauncherUpdateAsset asset,
        string targetPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var totalBytes = asset.SizeBytes > 0
            ? asset.SizeBytes
            : response.Content.Headers.ContentLength ?? -1;

        var network = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var file = new FileStream(
            targetPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous);

        try
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await network.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                hasher.AppendData(buffer, 0, read);
                received += read;
                if (totalBytes > 0)
                {
                    progress?.Report(Math.Clamp(received / (double)totalBytes, 0, 1));
                }
            }

            await file.FlushAsync(cancellationToken).ConfigureAwait(false);
            var hash = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            if (!string.Equals(hash, asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(targetPath);
                return Result<string>.Failure("Downloaded update failed checksum verification.");
            }

            if (asset.SizeBytes > 0 && received != asset.SizeBytes)
            {
                TryDelete(targetPath);
                return Result<string>.Failure("Downloaded update size did not match manifest.");
            }

            progress?.Report(1.0);
            return Result<string>.Success(targetPath);
        }
        finally
        {
            await network.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = false,
        })
        {
            Timeout = TimeSpan.FromMinutes(30),
        };
    }
}
