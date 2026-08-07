using System.Formats.Tar;
using System.IO.Compression;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Versions;

internal static class GamePackageFileOps
{
    internal static async Task<Result> ExtractAsync(
        GameVersionPackage package,
        string archivePath,
        string targetDir,
        ISandboxBrokerClient? broker,
        CancellationToken cancellationToken)
    {
        return package.Kind switch
        {
            ClientPackageKind.Zip => ExtractZip(archivePath, targetDir),
            ClientPackageKind.TarGz => await ExtractTarGzAsync(archivePath, targetDir, cancellationToken).ConfigureAwait(false),
            ClientPackageKind.WindowsInstaller => await ExtractWindowsInstallerAsync(
                archivePath,
                targetDir,
                broker,
                cancellationToken).ConfigureAwait(false),
            _ => Result.Failure("Unsupported package kind."),
        };
    }

    internal static void FlattenIfSingleRoot(string targetDir)
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

    private static async Task<Result> ExtractWindowsInstallerAsync(
        string archivePath,
        string targetDir,
        ISandboxBrokerClient? broker,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "/VERYSILENT",
            "/NORESTART",
            $"/DIR=\"{targetDir}\"",
        };

        if (broker is not null)
        {
            var brokerResult = await broker.RunInstallerAsync(archivePath, arguments, cancellationToken)
                .ConfigureAwait(false);
            if (!brokerResult.IsSuccess)
            {
                return brokerResult;
            }

            return WaitForInstallerDirectory(targetDir);
        }

        try
        {
            using var process = new global::System.Diagnostics.Process
            {
                StartInfo = new global::System.Diagnostics.ProcessStartInfo
                {
                    FileName = archivePath,
                    Arguments = string.Join(' ', arguments),
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

            return WaitForInstallerDirectory(targetDir);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static Result WaitForInstallerDirectory(string targetDir)
    {
        for (var i = 0; i < 120; i++)
        {
            if (Directory.Exists(targetDir) && Directory.EnumerateFileSystemEntries(targetDir).Any())
            {
                return Result.Success();
            }

            Thread.Sleep(500);
        }

        return Result.Failure("Windows installer did not create the target directory.");
    }
}
