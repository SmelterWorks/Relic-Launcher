using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Modpacks;

public sealed partial class ModpackService
{
    public Task<Result<IReadOnlyList<ModpackLocalSummary>>> ListLocalAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var root = GetLocalModpacksRoot();
            if (!Directory.Exists(root))
            {
                return Task.FromResult(Result<IReadOnlyList<ModpackLocalSummary>>.Success([]));
            }

            var list = new List<ModpackLocalSummary>();
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryAddLocalSummary(dir, list);
            }

            list.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
            return Task.FromResult(Result<IReadOnlyList<ModpackLocalSummary>>.Success(list));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Result<IReadOnlyList<ModpackLocalSummary>>.Failure(ex.Message));
        }
    }

    public async Task<Result<ModpackLocalSummary>> SaveLocalAsync(ModpackSaveRequest request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateSaveRequest(request);
        if (!validation.IsSuccess)
        {
            return Result<ModpackLocalSummary>.Failure(validation.Error!);
        }

        var mods = request.Mods
            .Where(m => !BuiltinModIds.IsBuiltin(m.ModId))
            .ToList();
        var packId = CreateLocalPackId(request.Name);
        var packDir = Path.Combine(GetLocalModpacksRoot(), packId);
        Directory.CreateDirectory(packDir);

        var tempZip = Path.Combine(Path.GetTempPath(), $"relic-modpack-save-{Guid.NewGuid():N}.relicmodpack");
        try
        {
            var export = await ExportAsync(new ModpackExportRequest
            {
                DestinationPath = tempZip,
                DataPath = request.DataPath,
                GameVersion = request.GameVersion,
                Name = request.Name,
                Description = request.Description,
                Mods = mods,
            }, cancellationToken).ConfigureAwait(false);

            if (!export.IsSuccess)
            {
                TryDeleteDirectory(packDir);
                return Result<ModpackLocalSummary>.Failure(export.Error ?? "Could not build modpack.");
            }

            if (export.Value!.Manifest.Distribution == ModpackDistribution.Offline)
            {
                ExtractOfflineModsFromZip(tempZip, packDir, cancellationToken);
            }

            var manifestJson = ModpackManifestCodec.Serialize(export.Value.Manifest);
            await File.WriteAllTextAsync(Path.Combine(packDir, ManifestFileName), manifestJson, cancellationToken).ConfigureAwait(false);

            return Result<ModpackLocalSummary>.Success(new ModpackLocalSummary
            {
                Id = packId,
                Name = export.Value.Manifest.Name,
                Description = export.Value.Manifest.Description,
                GameVersion = export.Value.Manifest.GameVersion,
                Distribution = export.Value.Manifest.Distribution,
                ModCount = export.Value.ModCount,
                CreatedAt = export.Value.Manifest.CreatedAt,
                DirectoryPath = packDir,
            });
        }
        finally
        {
            TryDelete(tempZip);
        }
    }

    public Task<Result> DeleteLocalAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packId))
        {
            return Task.FromResult(Result.Failure("Modpack id is required."));
        }

        var packDir = Path.Combine(GetLocalModpacksRoot(), packId);
        if (!Directory.Exists(packDir))
        {
            return Task.FromResult(Result.Failure("Modpack not found."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Delete(packDir, recursive: true);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Result.Failure(ex.Message));
        }
    }

    private void TryAddLocalSummary(string dir, List<ModpackLocalSummary> list)
    {
        var manifestPath = Path.Combine(dir, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = ValidateManifest(ModpackManifestCodec.Deserialize(json));
            var id = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            list.Add(new ModpackLocalSummary
            {
                Id = id,
                Name = manifest.Name,
                Description = manifest.Description,
                GameVersion = manifest.GameVersion,
                Distribution = manifest.Distribution,
                ModCount = manifest.Mods.Count,
                CreatedAt = manifest.CreatedAt,
                DirectoryPath = dir,
            });
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogDebug(ex, "Skipping invalid local modpack at {Dir}", dir);
        }
    }

    private static Result ValidateSaveRequest(ModpackSaveRequest request)
    {
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
            return Result.Failure("Select at least one mod to save.");
        }

        return Result.Success();
    }

    private static string CreateLocalPackId(string name)
    {
        var packId = $"{SanitizeArchiveName(name)}-{Guid.NewGuid():N}";
        return packId.Length > 48 ? packId[..48] : packId;
    }

    private static void ExtractOfflineModsFromZip(string tempZip, string packDir, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(tempZip);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.FullName.StartsWith(ModsArchivePrefix, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(entry.Name))
            {
                var destPath = Path.Combine(packDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                entry.ExtractToFile(destPath, overwrite: true);
            }
        }
    }
}
