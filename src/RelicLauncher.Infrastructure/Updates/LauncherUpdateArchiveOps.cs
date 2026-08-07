using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Versions;

namespace RelicLauncher.Infrastructure.Updates;

internal static class LauncherUpdateArchiveOps
{
    internal static Result ExtractZip(string archivePath, string targetDir)
    {
        try
        {
            ZipFile.ExtractToDirectory(archivePath, targetDir, overwriteFiles: true);
            GamePackageFileOps.FlattenIfSingleRoot(targetDir);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return Result.Failure(ex.Message);
        }
    }

    internal static Result ExtractTarGz(string archivePath, string targetDir)
    {
        try
        {
            using var file = File.OpenRead(archivePath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, targetDir, overwriteFiles: true);
            GamePackageFileOps.FlattenIfSingleRoot(targetDir);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return Result.Failure(ex.Message);
        }
    }
}
