using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Updates;

public sealed class LauncherUpdateApplyService : ILauncherUpdateApplyService
{
    private readonly LauncherUpdateDownloader _downloader;
    private readonly ILogger<LauncherUpdateApplyService> _logger;

    public LauncherUpdateApplyService(
        LauncherUpdateDownloader downloader,
        ILogger<LauncherUpdateApplyService> logger)
    {
        _downloader = downloader;
        _logger = logger;
    }

    public bool CanApplyInApp(LauncherInstallKind installKind)
    {
        return installKind is LauncherInstallKind.WindowsNsis
            or LauncherInstallKind.WindowsZip
            or LauncherInstallKind.LinuxPortableTar
            or LauncherInstallKind.LinuxAppImage
            or LauncherInstallKind.MacOsBundle;
    }

    public async Task<Result> DownloadAndApplyAsync(
        LauncherUpdateAsset asset,
        DetectedLauncherInstall install,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanApplyInApp(install.InstallKind))
        {
            return Result.Failure("This install type cannot be updated in-app.");
        }

        var download = await _downloader.DownloadVerifiedAsync(asset, progress, cancellationToken).ConfigureAwait(false);
        if (!download.IsSuccess)
        {
            return Result.Failure(download.Error ?? "Download failed.");
        }

        return install.InstallKind switch
        {
            LauncherInstallKind.WindowsNsis => ApplyWindowsNsis(download.Value!),
            LauncherInstallKind.WindowsZip => ApplyWindowsZip(download.Value!, install),
            LauncherInstallKind.LinuxPortableTar => ApplyLinuxPortableTar(download.Value!, install),
            LauncherInstallKind.LinuxAppImage => ApplyLinuxAppImage(download.Value!, install),
            LauncherInstallKind.MacOsBundle => ApplyMacOsBundle(download.Value!, install),
            _ => Result.Failure("Unsupported install type."),
        };
    }

    private Result ApplyWindowsNsis(string installerPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/S",
                UseShellExecute = true,
                Verb = "runas",
            };
            global::System.Diagnostics.Process.Start(startInfo);
            Environment.Exit(0);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or Win32Exception)
        {
            _logger.LogWarning(ex, "Failed to launch NSIS updater");
            return Result.Failure("Could not start the Windows installer.");
        }
    }

    private static Result ApplyWindowsZip(string archivePath, DetectedLauncherInstall install)
    {
        if (string.IsNullOrWhiteSpace(install.InstallDirectory))
        {
            return Result.Failure("Install directory is not known.");
        }

        return LauncherUpdateArchiveOps.ExtractZip(archivePath, install.InstallDirectory);
    }

    private static Result ApplyLinuxPortableTar(string archivePath, DetectedLauncherInstall install)
    {
        if (string.IsNullOrWhiteSpace(install.InstallDirectory))
        {
            return Result.Failure("Install directory is not known.");
        }

        var staging = Path.Combine(Path.GetTempPath(), $"relic-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        var extract = LauncherUpdateArchiveOps.ExtractTarGz(archivePath, staging);
        if (!extract.IsSuccess)
        {
            TryDeleteDirectory(staging);
            return extract;
        }

        try
        {
            CopyDirectoryContents(staging, install.InstallDirectory);
            return LaunchRestartHelper(install.ExecutablePath);
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    private static Result ApplyLinuxAppImage(string downloadedPath, DetectedLauncherInstall install)
    {
        var current = Environment.GetEnvironmentVariable("APPIMAGE");
        if (string.IsNullOrWhiteSpace(current))
        {
            current = install.ExecutablePath;
        }

        if (string.IsNullOrWhiteSpace(current))
        {
            return Result.Failure("Current AppImage path is not known.");
        }

        var target = Path.Combine(Path.GetDirectoryName(current) ?? ".", Path.GetFileName(downloadedPath));
        File.Copy(downloadedPath, target, overwrite: true);
        if (OperatingSystem.IsLinux())
        {
            global::System.IO.File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return LaunchRestartHelper(target);
    }

    private static Result ApplyMacOsBundle(string archivePath, DetectedLauncherInstall install)
    {
        if (string.IsNullOrWhiteSpace(install.InstallDirectory))
        {
            return Result.Failure("Application bundle path is not known.");
        }

        var staging = Path.Combine(Path.GetTempPath(), $"relic-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        var extract = archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
            ? LauncherUpdateArchiveOps.ExtractTarGz(archivePath, staging)
            : LauncherUpdateArchiveOps.ExtractZip(archivePath, staging);
        if (!extract.IsSuccess)
        {
            TryDeleteDirectory(staging);
            return extract;
        }

        try
        {
            var bundleRoot = Directory.EnumerateDirectories(staging, "*.app", SearchOption.AllDirectories).FirstOrDefault()
                ?? staging;
            CopyDirectoryContents(bundleRoot, install.InstallDirectory);
            return LaunchRestartHelper(install.ExecutablePath);
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    private static Result LaunchRestartHelper(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return Result.Failure("Executable path is not known for restart.");
        }

        try
        {
            var scriptPath = Path.Combine(Path.GetTempPath(), $"relic-restart-{Guid.NewGuid():N}.sh");
            var content = "#!/bin/sh\n" +
                          "sleep 1\n" +
                          $"exec \"{executablePath.Replace("\"", "\\\"")}\" \"$@\"\n";
            File.WriteAllText(scriptPath, content);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            global::System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = scriptPath,
                UseShellExecute = false,
            });

            Environment.Exit(0);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static void CopyDirectoryContents(string sourceDir, string targetDir)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(targetDir, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destination = Path.Combine(targetDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
