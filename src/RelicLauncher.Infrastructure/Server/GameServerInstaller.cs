using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Versions;

namespace RelicLauncher.Infrastructure.Server;

public sealed class GameServerInstaller : IGameServerInstaller, IDisposable
{
    private readonly IAppPathProvider _pathProvider;
    private readonly IInstalledServerStore _installedStore;
    private readonly IRuntimePlatform _platform;
    private readonly GameVersionInstaller _packageInstaller;
    private readonly ILogger<GameServerInstaller> _logger;

    public GameServerInstaller(
        IAppPathProvider pathProvider,
        IInstalledServerStore installedStore,
        IRuntimePlatform platform,
        GameVersionInstaller packageInstaller,
        ILogger<GameServerInstaller> logger)
    {
        _pathProvider = pathProvider;
        _installedStore = installedStore;
        _platform = platform;
        _packageInstaller = packageInstaller;
        _logger = logger;
    }

    public void Dispose() => _packageInstaller.Dispose();

    public GameVersionPackage? SelectServerPackage(GameVersionInfo version, PlatformInfo platform)
    {
        var key = platform.ServerPackageKey;
        return version.Packages.FirstOrDefault(p => string.Equals(p.PlatformKey, key, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Result<InstalledServerVersion>> InstallAsync(ServerInstallRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InstallsRoot))
        {
            return Result<InstalledServerVersion>.Failure("Installs root is not configured.");
        }

        var platform = _platform.GetPlatformInfo();
        var package = SelectServerPackage(request.Version, platform);
        if (package is null)
        {
            return Result<InstalledServerVersion>.Failure($"No server package found for {platform.ServerPackageKey}.");
        }

        try
        {
            return await InstallCoreAsync(request, package, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Server install failed for {Version}", request.Version.Version);
            return Result<InstalledServerVersion>.Failure(ex.Message);
        }
    }

    private async Task<Result<InstalledServerVersion>> InstallCoreAsync(
        ServerInstallRequest request,
        GameVersionPackage package,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(request.InstallsRoot);
        var cacheDir = Path.Combine(_pathProvider.GetPaths().CacheDirectory, "downloads");
        Directory.CreateDirectory(cacheDir);
        var archivePath = Path.Combine(cacheDir, package.FileName);

        var download = await _packageInstaller.DownloadAsync(package, archivePath, request.Progress, cancellationToken)
            .ConfigureAwait(false);
        if (!download.IsSuccess)
        {
            return Result<InstalledServerVersion>.Failure(download.Error ?? "Download failed.");
        }

        var verify = await VerifyMd5Async(package, archivePath, cancellationToken).ConfigureAwait(false);
        if (!verify.IsSuccess)
        {
            return Result<InstalledServerVersion>.Failure(verify.Error!);
        }

        var targetDir = GameServerInstallLayout.GetServerDirectory(request.InstallsRoot, request.Version.Version);
        if (Directory.Exists(targetDir))
        {
            Directory.Delete(targetDir, recursive: true);
        }

        Directory.CreateDirectory(targetDir);
        var extract = await GamePackageFileOps.ExtractAsync(package, archivePath, targetDir, cancellationToken)
            .ConfigureAwait(false);
        if (!extract.IsSuccess)
        {
            return Result<InstalledServerVersion>.Failure(extract.Error ?? "Extract failed.");
        }

        GamePackageFileOps.FlattenIfSingleRoot(targetDir);
        var installed = BuildInstalled(request.Version.Version, targetDir);
        if (!installed.ExecutableFound)
        {
            return Result<InstalledServerVersion>.Failure("Install finished but no server executable was found.");
        }

        await SaveInstalledAsync(request.InstallsRoot, installed, cancellationToken).ConfigureAwait(false);
        request.Progress?.Report(1.0);
        return Result<InstalledServerVersion>.Success(installed);
    }

    private static InstalledServerVersion BuildInstalled(string version, string targetDir)
    {
        var exe = VintageStoryServerExecutableLocator.FindServerExecutable(targetDir);
        return new InstalledServerVersion
        {
            Version = version,
            InstallPath = targetDir,
            ExecutablePath = exe,
            ExecutableFound = exe is not null,
            InstalledAt = DateTimeOffset.UtcNow,
        };
    }

    private async Task SaveInstalledAsync(string installsRoot, InstalledServerVersion installed, CancellationToken cancellationToken)
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

        var hash = await GameVersionInstaller.ComputeMd5Async(archivePath, cancellationToken).ConfigureAwait(false);
        return string.Equals(hash, package.Md5, StringComparison.OrdinalIgnoreCase)
            ? Result.Success()
            : Result.Failure("Downloaded file failed MD5 verification.");
    }

    public async Task<Result> UninstallAsync(string installsRoot, string version, CancellationToken cancellationToken = default)
    {
        try
        {
            var dir = GameServerInstallLayout.GetServerDirectory(installsRoot, version);
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
}
