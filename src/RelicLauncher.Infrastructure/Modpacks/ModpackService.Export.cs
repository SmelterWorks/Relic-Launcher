using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Modpacks;

public sealed partial class ModpackService
{
    public async Task<Result<ModpackSummary>> ExportAsync(ModpackExportRequest request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateExportRequest(request);
        if (!validation.IsSuccess)
        {
            return Result<ModpackSummary>.Failure(validation.Error!);
        }

        var mods = request.Mods
            .Where(m => !BuiltinModIds.IsBuiltin(m.ModId))
            .ToList();
        var (manifest, entries) = BuildExportManifest(request, mods);
        var tempPath = request.DestinationPath + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.DestinationPath))!);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            await WriteExportArchiveAsync(tempPath, mods, entries, manifest, request.Progress, cancellationToken).ConfigureAwait(false);

            if (File.Exists(request.DestinationPath))
            {
                File.Delete(request.DestinationPath);
            }

            File.Move(tempPath, request.DestinationPath);
            request.Progress?.Report(1.0);

            return Result<ModpackSummary>.Success(new ModpackSummary
            {
                Path = request.DestinationPath,
                Manifest = manifest,
                ModCount = mods.Count,
                TotalBytes = new FileInfo(request.DestinationPath).Length,
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            TryDelete(tempPath);
            _logger.LogWarning(ex, "Modpack export failed for {Destination}", request.DestinationPath);
            return Result<ModpackSummary>.Failure("Could not export modpack: " + ex.Message);
        }
    }

    public async Task<Result<ModpackSummary>> ExportSavedAsync(
        string packDirectory,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packDirectory) || !Directory.Exists(packDirectory))
        {
            return Result<ModpackSummary>.Failure("Modpack directory not found.");
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return Result<ModpackSummary>.Failure("Choose a destination for the modpack file.");
        }

        var manifestPath = Path.Combine(packDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return Result<ModpackSummary>.Failure("Manifest not found in modpack directory.");
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            var manifest = ValidateManifest(ModpackManifestCodec.Deserialize(json));
            var tempPath = destinationPath + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
            using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                if (manifest.Distribution == ModpackDistribution.Offline)
                {
                    AddOfflineModsFromDirectory(archive, packDirectory, cancellationToken);
                }

                WriteManifestJsonEntry(archive, json);
            }

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(tempPath, destinationPath);
            return Result<ModpackSummary>.Success(new ModpackSummary
            {
                Path = destinationPath,
                Manifest = manifest,
                ModCount = manifest.Mods.Count,
                TotalBytes = new FileInfo(destinationPath).Length,
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Modpack export failed for saved pack {Directory}", packDirectory);
            return Result<ModpackSummary>.Failure("Could not export modpack: " + ex.Message);
        }
    }

    private static Result ValidateExportRequest(ModpackExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DestinationPath))
        {
            return Result.Failure("Choose a destination for the modpack file.");
        }

        if (string.IsNullOrWhiteSpace(request.DataPath))
        {
            return Result.Failure("Vintage Story data path is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.GameVersion))
        {
            return Result.Failure("Game version is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure("Modpack name is required.");
        }

        if (request.Mods.Count(m => !BuiltinModIds.IsBuiltin(m.ModId)) == 0)
        {
            return Result.Failure("Select at least one mod to export.");
        }

        return Result.Success();
    }

    private (ModpackManifest Manifest, List<ModpackModEntry> Entries) BuildExportManifest(
        ModpackExportRequest request,
        List<LocalModInfo> mods)
    {
        var origins = mods.Select(_originResolver.Resolve).ToList();
        var distribution = ModOriginClassifier.RequiresOfflinePack(origins)
            ? ModpackDistribution.Offline
            : ModpackDistribution.Online;

        var entries = new List<ModpackModEntry>();
        for (var i = 0; i < mods.Count; i++)
        {
            var mod = mods[i];
            var origin = origins[i];
            var modId = mod.ModId?.Trim() ?? $"mod-{i}";
            entries.Add(new ModpackModEntry
            {
                ModId = modId,
                ModVersion = mod.Version,
                FileId = origin.FileId,
                Enabled = mod.IsEnabled,
                Source = origin.Source,
                ArchivePath = distribution == ModpackDistribution.Offline
                    ? $"{ModsArchivePrefix}{SanitizeArchiveName(modId)}.zip"
                    : null,
            });
        }

        var manifest = new ModpackManifest
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            GameVersion = request.GameVersion.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            RelicVersion = BuildMetadata.Version,
            Distribution = distribution,
            Mods = entries,
        };

        return (manifest, entries);
    }

    private async Task WriteExportArchiveAsync(
        string tempPath,
        List<LocalModInfo> mods,
        List<ModpackModEntry> entries,
        ModpackManifest manifest,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create);
        if (manifest.Distribution == ModpackDistribution.Offline)
        {
            await AddOfflineModsToArchiveAsync(archive, mods, entries, progress, cancellationToken).ConfigureAwait(false);
        }

        WriteManifestEntry(archive, manifest);
    }

    private async Task AddOfflineModsToArchiveAsync(
        ZipArchive archive,
        List<LocalModInfo> mods,
        List<ModpackModEntry> entries,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < mods.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mod = mods[i];
            var entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.ArchivePath))
            {
                continue;
            }

            var tempZip = Path.Combine(Path.GetTempPath(), $"relic-modpack-{Guid.NewGuid():N}.zip");
            try
            {
                await CreateModArchiveAsync(mod, tempZip, cancellationToken).ConfigureAwait(false);
                archive.CreateEntryFromFile(tempZip, entry.ArchivePath!, CompressionLevel.Optimal);
            }
            finally
            {
                TryDelete(tempZip);
            }

            ReportProgress(progress, (i + 1) / (mods.Count + 1));
        }
    }

    private static void AddOfflineModsFromDirectory(ZipArchive archive, string packDirectory, CancellationToken cancellationToken)
    {
        var modsDir = Path.Combine(packDirectory, "mods");
        if (!Directory.Exists(modsDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(modsDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(modsDir, file).Replace(Path.DirectorySeparatorChar, '/');
            archive.CreateEntryFromFile(file, $"{ModsArchivePrefix}{relative}", CompressionLevel.Optimal);
        }
    }

    private static async Task CreateModArchiveAsync(LocalModInfo mod, string zipPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mod.IsDirectory)
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(mod.Path, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return;
        }

        if (!File.Exists(mod.Path))
        {
            throw new IOException($"Mod file not found: {mod.Path}");
        }

        File.Copy(mod.Path, zipPath, overwrite: true);
    }
}
