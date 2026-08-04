using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Auth;

namespace RelicLauncher.Infrastructure.Versions;

public sealed class GameVersionInstaller : IGameVersionInstaller
{
    private readonly IAppPathProvider _pathProvider;
    private readonly IInstalledVersionStore _installedStore;
    private readonly IRuntimePlatform _platform;
    private readonly AccountAuthService _auth;
    private readonly ILogger<GameVersionInstaller> _logger;

    public GameVersionInstaller(
        IAppPathProvider pathProvider,
        IInstalledVersionStore installedStore,
        IRuntimePlatform platform,
        IAccountAuthService auth,
        ILogger<GameVersionInstaller> logger)
    {
        _pathProvider = pathProvider;
        _installedStore = installedStore;
        _platform = platform;
        _auth = auth as AccountAuthService
            ?? throw new InvalidOperationException("AccountAuthService implementation is required for downloads.");
        _logger = logger;
    }

    public GameVersionPackage? SelectClientPackage(GameVersionInfo version, PlatformInfo platform)
    {
        var key = platform.ClientPackageKey;
        var match = version.Packages.FirstOrDefault(p => string.Equals(p.PlatformKey, key, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match;
        }

        return version.Packages.FirstOrDefault(p => p.Kind is ClientPackageKind.TarGz or ClientPackageKind.Zip);
    }

    public async Task<Result<InstalledGameVersion>> InstallAsync(VersionInstallRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InstallsRoot))
        {
            return Result<InstalledGameVersion>.Failure("Installs root is not configured.");
        }

        var platform = _platform.GetPlatformInfo();
        var package = SelectClientPackage(request.Version, platform);
        if (package is null)
        {
            return Result<InstalledGameVersion>.Failure($"No client package found for {platform.ClientPackageKey}.");
        }

        var auth = await _auth.EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        if (!auth.IsSuccess)
        {
            return Result<InstalledGameVersion>.Failure(auth.Error ?? "Sign-in required.");
        }

        try
        {
            return await InstallCoreAsync(request, package, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Version install failed for {Version}", request.Version.Version);
            return Result<InstalledGameVersion>.Failure(ex.Message);
        }
    }

    private async Task<Result<InstalledGameVersion>> InstallCoreAsync(
        VersionInstallRequest request,
        GameVersionPackage package,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(request.InstallsRoot);
        var cacheDir = Path.Combine(_pathProvider.GetPaths().CacheDirectory, "downloads");
        Directory.CreateDirectory(cacheDir);
        var archivePath = Path.Combine(cacheDir, package.FileName);

        var download = await DownloadAsync(package, archivePath, request.Progress, cancellationToken).ConfigureAwait(false);
        if (!download.IsSuccess)
        {
            return Result<InstalledGameVersion>.Failure(download.Error ?? "Download failed.");
        }

        var verify = await VerifyMd5Async(package, archivePath, cancellationToken).ConfigureAwait(false);
        if (!verify.IsSuccess)
        {
            return Result<InstalledGameVersion>.Failure(verify.Error!);
        }

        var targetDir = GameInstallLayout.GetVersionDirectory(request.InstallsRoot, request.Version.Version);
        if (Directory.Exists(targetDir))
        {
            Directory.Delete(targetDir, recursive: true);
        }

        Directory.CreateDirectory(targetDir);
        var extract = await ExtractAsync(package, archivePath, targetDir, cancellationToken).ConfigureAwait(false);
        if (!extract.IsSuccess)
        {
            return Result<InstalledGameVersion>.Failure(extract.Error ?? "Extract failed.");
        }

        FlattenIfSingleRoot(targetDir);
        var installed = BuildInstalled(request.Version.Version, targetDir);
        if (!installed.ExecutableFound)
        {
            return Result<InstalledGameVersion>.Failure("Install finished but no client executable was found.");
        }

        await SaveInstalledAsync(request.InstallsRoot, installed, cancellationToken).ConfigureAwait(false);
        request.Progress?.Report(1.0);
        return Result<InstalledGameVersion>.Success(installed);
    }

    private static InstalledGameVersion BuildInstalled(string version, string targetDir)
    {
        var exe = VintageStoryExecutableLocator.FindClientExecutable(targetDir);
        return new InstalledGameVersion
        {
            Version = version,
            InstallPath = targetDir,
            ExecutablePath = exe,
            ExecutableFound = exe is not null,
            InstalledAt = DateTimeOffset.UtcNow,
        };
    }

    private async Task SaveInstalledAsync(string installsRoot, InstalledGameVersion installed, CancellationToken cancellationToken)
    {
        var existing = await _installedStore.ListAsync(installsRoot, cancellationToken).ConfigureAwait(false);
        var list = existing.IsSuccess
            ? existing.Value!.Where(v => !string.Equals(v.Version, installed.Version, StringComparison.OrdinalIgnoreCase)).ToList()
            : [];
        list.Add(installed);
        await _installedStore.SaveAsync(installsRoot, list, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> VerifyMd5Async(GameVersionPackage package, string archivePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(package.Md5))
        {
            return Result.Success();
        }

        var hash = await ComputeMd5Async(archivePath, cancellationToken).ConfigureAwait(false);
        return string.Equals(hash, package.Md5, StringComparison.OrdinalIgnoreCase)
            ? Result.Success()
            : Result.Failure("Downloaded file failed MD5 verification.");
    }

    public async Task<Result> UninstallAsync(string installsRoot, string version, CancellationToken cancellationToken = default)
    {
        try
        {
            var dir = GameInstallLayout.GetVersionDirectory(installsRoot, version);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }

            var existing = await _installedStore.ListAsync(installsRoot, cancellationToken).ConfigureAwait(false);
            if (existing.IsSuccess)
            {
                var list = existing.Value!
                    .Where(v => !string.Equals(v.Version, version, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                await _installedStore.SaveAsync(installsRoot, list, cancellationToken).ConfigureAwait(false);
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure(ex.Message);
        }
    }

    private async Task<Result> DownloadAsync(
        GameVersionPackage package,
        string destination,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var urls = new[] { package.CdnUrl, package.LocalUrl }
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Exception? last = null;
        foreach (var url in urls)
        {
            try
            {
                using var response = await GetWithRedirectsAsync(url!, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    last = new HttpRequestException($"HTTP {(int)response.StatusCode} for {url}");
                    continue;
                }

                var total = response.Content.Headers.ContentLength;
                using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                var buffer = new byte[81920];
                long readTotal = 0;
                int read;
                while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    readTotal += read;
                    if (total is > 0)
                    {
                        progress?.Report(Math.Clamp(0.9 * (readTotal / (double)total.Value), 0, 0.9));
                    }
                    else
                    {
                        progress?.Report(0.45);
                    }
                }

                progress?.Report(0.9);
                return Result.Success();
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                last = ex;
            }
        }

        return Result.Failure(last?.Message ?? "Download failed.");
    }

    private async Task<HttpResponseMessage> GetWithRedirectsAsync(string url, CancellationToken cancellationToken)
    {
        var current = url;
        for (var hop = 0; hop < 8; hop++)
        {
            var response = await _auth.HttpClient.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
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

    private static async Task<Result> ExtractAsync(
        GameVersionPackage package,
        string archivePath,
        string targetDir,
        CancellationToken cancellationToken)
    {
        return package.Kind switch
        {
            ClientPackageKind.Zip => ExtractZip(archivePath, targetDir),
            ClientPackageKind.TarGz => await ExtractTarGzAsync(archivePath, targetDir, cancellationToken).ConfigureAwait(false),
            ClientPackageKind.WindowsInstaller => ExtractWindowsInstaller(archivePath, targetDir),
            _ => Result.Failure("Unsupported package kind."),
        };
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

    private static Result ExtractWindowsInstaller(string archivePath, string targetDir)
    {
        try
        {
            using var process = new global::System.Diagnostics.Process
            {
                StartInfo = new global::System.Diagnostics.ProcessStartInfo
                {
                    FileName = archivePath,
                    Arguments = $"/VERYSILENT /NORESTART /DIR=\"{targetDir}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            if (!process.Start())
            {
                return Result.Failure("Could not start Windows installer.");
            }

            process.WaitForExit(TimeSpan.FromMinutes(30));
            if (process.ExitCode != 0)
            {
                return Result.Failure($"Windows installer exited with code {process.ExitCode}.");
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static void FlattenIfSingleRoot(string targetDir)
    {
        var entries = Directory.GetFileSystemEntries(targetDir);
        if (entries.Length != 1 || !Directory.Exists(entries[0]))
        {
            return;
        }

        var nested = entries[0];
        foreach (var child in Directory.GetFileSystemEntries(nested))
        {
            var name = Path.GetFileName(child);
            var dest = Path.Combine(targetDir, name);
            if (Directory.Exists(child))
            {
                Directory.Move(child, dest);
            }
            else
            {
                File.Move(child, dest, overwrite: true);
            }
        }

        Directory.Delete(nested, recursive: true);
    }

    private static async Task<string> ComputeMd5Async(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
