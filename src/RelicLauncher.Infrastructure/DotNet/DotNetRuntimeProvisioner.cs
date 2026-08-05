using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.IO;

namespace RelicLauncher.Infrastructure.DotNet;

public sealed class DotNetRuntimeProvisioner : IDotNetRuntimeProvisioner, IDisposable
{
    private const string AzureFeed = "https://builds.dotnet.microsoft.com/dotnet";

    private readonly IAppPathProvider _pathProvider;
    private readonly IRuntimePlatform _platform;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DotNetRuntimeProvisioner> _logger;
    private readonly Func<IReadOnlyList<string>> _systemRootFactory;

    public DotNetRuntimeProvisioner(
        IAppPathProvider pathProvider,
        IRuntimePlatform platform,
        ILogger<DotNetRuntimeProvisioner> logger)
        : this(pathProvider, platform, logger, CreateHttpClient(), EnumerateDefaultSystemRoots)
    {
    }

    internal DotNetRuntimeProvisioner(
        IAppPathProvider pathProvider,
        IRuntimePlatform platform,
        ILogger<DotNetRuntimeProvisioner> logger,
        HttpClient httpClient,
        Func<IReadOnlyList<string>>? systemRootFactory = null)
    {
        _pathProvider = pathProvider;
        _platform = platform;
        _logger = logger;
        _httpClient = httpClient;
        _systemRootFactory = systemRootFactory ?? EnumerateDefaultSystemRoots;
    }

    public void Dispose() => _httpClient.Dispose();

    public async Task<Result<DotNetRuntimeResolveInfo>> EnsureAsync(
        int majorVersion,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (majorVersion is not (7 or 8 or 10))
        {
            return Result<DotNetRuntimeResolveInfo>.Failure(
                $"Unsupported .NET major version {majorVersion}.");
        }

        var platform = _platform.GetPlatformInfo();
        var rid = DotNetRidMapper.TryMap(platform.Os, platform.Arch);
        if (rid is null)
        {
            return Result<DotNetRuntimeResolveInfo>.Failure(
                $"No .NET runtime package is available for {platform.Os}/{platform.Arch}.");
        }

        var requireDesktop = DotNetRidMapper.RequiresWindowsDesktop(platform.Os);
        var managedRoot = DotNetRuntimeLayout.GetManagedRoot(_pathProvider.GetPaths().CacheDirectory, majorVersion);

        var existing = FindInstalledRoot(majorVersion, requireDesktop, managedRoot);
        if (existing is not null)
        {
            progress?.Report(1.0);
            return Result<DotNetRuntimeResolveInfo>.Success(existing);
        }

        try
        {
            var acquired = await AcquireManagedRuntimeAsync(
                majorVersion,
                rid,
                requireDesktop,
                managedRoot,
                progress,
                cancellationToken).ConfigureAwait(false);
            if (!acquired.IsSuccess)
            {
                return Result<DotNetRuntimeResolveInfo>.Failure(acquired.Error ?? "Runtime download failed.");
            }

            progress?.Report(1.0);
            return Result<DotNetRuntimeResolveInfo>.Success(new DotNetRuntimeResolveInfo
            {
                DotNetRoot = managedRoot,
                IsManagedByRelic = true,
                MajorVersion = majorVersion,
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Failed to provision .NET {Major}", majorVersion);
            return Result<DotNetRuntimeResolveInfo>.Failure(ex.Message);
        }
    }

    private DotNetRuntimeResolveInfo? FindInstalledRoot(int majorVersion, bool requireDesktop, string managedRoot)
    {
        if (DotNetRuntimeLayout.HasRequiredSharedFrameworks(managedRoot, majorVersion, requireDesktop))
        {
            return new DotNetRuntimeResolveInfo
            {
                DotNetRoot = managedRoot,
                IsManagedByRelic = true,
                MajorVersion = majorVersion,
            };
        }

        foreach (var root in _systemRootFactory())
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            if (!DotNetRuntimeLayout.HasRequiredSharedFrameworks(root, majorVersion, requireDesktop))
            {
                continue;
            }

            return new DotNetRuntimeResolveInfo
            {
                DotNetRoot = root,
                IsManagedByRelic = false,
                MajorVersion = majorVersion,
            };
        }

        return null;
    }

    private async Task<Result> AcquireManagedRuntimeAsync(
        int majorVersion,
        string rid,
        bool requireDesktop,
        string managedRoot,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var channel = $"{majorVersion}.0";
        var product = requireDesktop ? "WindowsDesktop" : "Runtime";
        var versionResult = await ResolveLatestPatchAsync(product, channel, cancellationToken).ConfigureAwait(false);
        if (!versionResult.IsSuccess)
        {
            return Result.Failure(versionResult.Error ?? "Could not resolve runtime version.");
        }

        var version = versionResult.Value!;
        var fileName = requireDesktop
            ? $"windowsdesktop-runtime-{version}-{rid}.zip"
            : $"dotnet-runtime-{version}-{rid}.tar.gz";
        var url = $"{AzureFeed}/{product}/{version}/{fileName}";

        var downloadDir = DotNetRuntimeLayout.GetDownloadDirectory(_pathProvider.GetPaths().CacheDirectory);
        Directory.CreateDirectory(downloadDir);
        var archivePath = Path.Combine(downloadDir, fileName);

        var download = await DownloadAsync(url, archivePath, progress, cancellationToken).ConfigureAwait(false);
        if (!download.IsSuccess)
        {
            return download;
        }

        if (Directory.Exists(managedRoot))
        {
            Directory.Delete(managedRoot, recursive: true);
        }

        Directory.CreateDirectory(managedRoot);
        var extract = requireDesktop
            ? ExtractZip(archivePath, managedRoot)
            : await ExtractTarGzAsync(archivePath, managedRoot, cancellationToken).ConfigureAwait(false);
        if (!extract.IsSuccess)
        {
            return extract;
        }

        if (!DotNetRuntimeLayout.HasRequiredSharedFrameworks(managedRoot, majorVersion, requireDesktop))
        {
            return Result.Failure("Downloaded runtime is missing required shared frameworks.");
        }

        return Result.Success();
    }

    private async Task<Result<string>> ResolveLatestPatchAsync(
        string product,
        string channel,
        CancellationToken cancellationToken)
    {
        var url = $"{AzureFeed}/{product}/{channel}/latest.version";
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Result<string>.Failure($"Could not resolve {product} {channel} (HTTP {(int)response.StatusCode}).");
        }

        var text = (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<string>.Failure($"Empty version response for {product} {channel}.");
        }

        return Result<string>.Success(text);
    }

    private async Task<Result> DownloadAsync(
        string url,
        string destination,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await GetWithRedirectsAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure($"HTTP {(int)response.StatusCode} for runtime download.");
        }

        var total = response.Content.Headers.ContentLength;
        using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        var copy = await BoundedStreamCopy.CopyAsync(
            input,
            output,
            total,
            RelicDefaults.MaxDotNetRuntimeDownloadBytes,
            new Progress<double>(value => progress?.Report(Math.Clamp(0.9 * value, 0, 0.9))),
            cancellationToken).ConfigureAwait(false);
        if (!copy.IsSuccess)
        {
            TryDelete(destination);
            return copy;
        }

        progress?.Report(0.9);
        return Result.Success();
    }

    private async Task<HttpResponseMessage> GetWithRedirectsAsync(string url, CancellationToken cancellationToken)
    {
        var current = url;
        for (var hop = 0; hop < 8; hop++)
        {
            var response = await _httpClient.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if ((int)response.StatusCode is < 300 or >= 400)
            {
                return response;
            }

            var location = response.Headers.Location;
            response.Dispose();
            if (location is null)
            {
                throw new HttpRequestException($"Redirect without Location for {current}");
            }

            current = location.IsAbsoluteUri ? location.ToString() : new Uri(new Uri(current), location).ToString();
        }

        throw new HttpRequestException($"Too many redirects for {url}");
    }

    private static Result ExtractZip(string archivePath, string targetDir)
    {
        try
        {
            ZipFile.ExtractToDirectory(archivePath, targetDir, overwriteFiles: true);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static Task<Result> ExtractTarGzAsync(string archivePath, string targetDir, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var file = File.OpenRead(archivePath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, targetDir, overwriteFiles: true);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException)
        {
            return Task.FromResult(Result.Failure(ex.Message));
        }
    }

    internal static IReadOnlyList<string> EnumerateDefaultSystemRoots()
    {
        var roots = new List<string>();
        var envRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot))
        {
            roots.Add(envRoot);
        }

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                roots.Add(Path.Combine(programFiles, "dotnet"));
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            roots.Add("/usr/local/share/dotnet");
            roots.Add("/opt/homebrew/opt/dotnet/libexec");
            roots.Add("/usr/local/opt/dotnet/libexec");
        }
        else
        {
            roots.Add("/usr/share/dotnet");
            roots.Add("/usr/lib/dotnet");
            roots.Add("/usr/local/share/dotnet");
        }

        return roots;
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
            // Best effort cleanup.
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = false,
        })
        {
            Timeout = TimeSpan.FromMinutes(30),
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("RelicLauncher", BuildMetadata.Version));
        return client;
    }
}
