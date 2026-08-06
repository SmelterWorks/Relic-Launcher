using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Backup;

public sealed class BackupService : IBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IInstalledVersionStore _installedStore;
    private readonly ILogger<BackupService> _logger;

    public BackupService(IInstalledVersionStore installedStore, ILogger<BackupService> logger)
    {
        _installedStore = installedStore;
        _logger = logger;
    }

    public async Task<Result<BackupSummary>> CreateAsync(BackupRequest request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateRequest(request);
        if (!validation.IsSuccess)
        {
            return Result<BackupSummary>.Failure(validation.Error!);
        }

        var tempPath = request.DestinationZipPath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.DestinationZipPath))!);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            var (totalBytes, fileCount) = await WriteArchiveAsync(request, tempPath, cancellationToken).ConfigureAwait(false);

            if (File.Exists(request.DestinationZipPath))
            {
                File.Delete(request.DestinationZipPath);
            }

            File.Move(tempPath, request.DestinationZipPath);
            request.Progress?.Report(1.0);
            return Result<BackupSummary>.Success(new BackupSummary
            {
                ZipPath = request.DestinationZipPath,
                IncludedMods = request.IncludeMods,
                IncludedWorlds = request.IncludeWorlds,
                IncludedVersions = request.VersionsToInclude,
                TotalBytes = totalBytes,
                FileCount = fileCount,
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            TryDelete(tempPath);
            _logger.LogWarning(ex, "Backup creation failed for {Destination}", request.DestinationZipPath);
            return Result<BackupSummary>.Failure("Could not create backup: " + ex.Message);
        }
    }

    private async Task<(long TotalBytes, int FileCount)> WriteArchiveAsync(
        BackupRequest request,
        string tempPath,
        CancellationToken cancellationToken)
    {
        long totalBytes = 0;
        var fileCount = 0;

        using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
        {
            if (request.IncludeMods)
            {
                (totalBytes, fileCount) = AddDirectory(archive, GameInstallLayout.GetModsDirectory(request.DataPath!), "data/Mods", totalBytes, fileCount, cancellationToken);
                (totalBytes, fileCount) = AddDirectory(archive, GameInstallLayout.GetModConfigDirectory(request.DataPath!), "data/ModConfig", totalBytes, fileCount, cancellationToken);
            }

            if (request.IncludeWorlds)
            {
                (totalBytes, fileCount) = AddDirectory(archive, GameInstallLayout.GetSavesDirectory(request.DataPath!), "data/Saves", totalBytes, fileCount, cancellationToken);
                (totalBytes, fileCount) = AddDirectory(archive, GameInstallLayout.GetBackupSavesDirectory(request.DataPath!), "data/BackupSaves", totalBytes, fileCount, cancellationToken);
            }

            foreach (var version in request.VersionsToInclude)
            {
                var versionDir = GameInstallLayout.GetVersionDirectory(request.InstallsRoot!, version);
                (totalBytes, fileCount) = AddDirectory(archive, versionDir, $"versions/{version}", totalBytes, fileCount, cancellationToken);
            }

            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            var manifestStream = manifestEntry.Open();
            await using (manifestStream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(manifestStream, BuildManifest(request), JsonOptions, cancellationToken).ConfigureAwait(false);
            }
        }

        return (totalBytes, fileCount);
    }

    private static BackupManifest BuildManifest(BackupRequest request)
        => new()
        {
            CreatedAt = DateTimeOffset.UtcNow,
            RelicVersion = BuildMetadata.Version,
            IncludesMods = request.IncludeMods,
            IncludesWorlds = request.IncludeWorlds,
            Versions = request.VersionsToInclude,
        };

    private static Result ValidateCreateRequest(BackupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DestinationZipPath))
        {
            return Result.Failure("Choose a destination for the backup file.");
        }

        if (!request.IncludeMods && !request.IncludeWorlds && request.VersionsToInclude.Count == 0)
        {
            return Result.Failure("Select at least one thing to back up.");
        }

        if ((request.IncludeMods || request.IncludeWorlds) && string.IsNullOrWhiteSpace(request.DataPath))
        {
            return Result.Failure("Vintage Story data path is not configured.");
        }

        if (request.VersionsToInclude.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(request.InstallsRoot))
            {
                return Result.Failure("Installs root is not configured.");
            }

            foreach (var version in request.VersionsToInclude)
            {
                if (!Directory.Exists(GameInstallLayout.GetVersionDirectory(request.InstallsRoot, version)))
                {
                    return Result.Failure($"Version {version} is not installed.");
                }
            }
        }

        return Result.Success();
    }

    private static (long TotalBytes, int FileCount) AddDirectory(
        ZipArchive archive,
        string sourceDir,
        string entryPrefix,
        long totalBytes,
        int fileCount,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sourceDir))
        {
            return (totalBytes, fileCount);
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(sourceDir, file).Replace(Path.DirectorySeparatorChar, '/');
            archive.CreateEntryFromFile(file, $"{entryPrefix}/{relative}", CompressionLevel.Optimal);
            totalBytes += new FileInfo(file).Length;
            fileCount++;
        }

        return (totalBytes, fileCount);
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
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }

    public async Task<Result<BackupRestoreSummary>> RestoreAsync(BackupRestoreRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceZipPath) || !File.Exists(request.SourceZipPath))
        {
            return Result<BackupRestoreSummary>.Failure("Backup file not found.");
        }

        try
        {
            using var archive = ZipFile.OpenRead(request.SourceZipPath);
            var progress = new BackupRestoreProgress();
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var extract = await RestoreEntryAsync(entry, request, progress, cancellationToken).ConfigureAwait(false);
                if (!extract.IsSuccess)
                {
                    return Result<BackupRestoreSummary>.Failure(extract.Error!);
                }
            }

            if (progress.RestoredVersions.Count > 0)
            {
                await UpdateInstalledVersionsAsync(request.InstallsRoot!, progress.RestoredVersions, cancellationToken).ConfigureAwait(false);
            }

            request.Progress?.Report(1.0);
            return Result<BackupRestoreSummary>.Success(new BackupRestoreSummary
            {
                RestoredMods = progress.RestoredMods,
                RestoredWorlds = progress.RestoredWorlds,
                RestoredVersions = progress.RestoredVersions,
                FileCount = progress.FileCount,
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _logger.LogWarning(ex, "Backup restore failed for {Source}", request.SourceZipPath);
            return Result<BackupRestoreSummary>.Failure("Could not restore backup: " + ex.Message);
        }
    }

    private async Task<Result> RestoreEntryAsync(
        ZipArchiveEntry entry,
        BackupRestoreRequest request,
        BackupRestoreProgress progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(entry.Name) || string.Equals(entry.FullName, "manifest.json", StringComparison.Ordinal))
        {
            return Result.Success();
        }

        if (entry.FullName.StartsWith("data/", StringComparison.Ordinal))
        {
            return await RestoreDataEntryAsync(entry, request, progress, cancellationToken).ConfigureAwait(false);
        }

        if (entry.FullName.StartsWith("versions/", StringComparison.Ordinal))
        {
            return await RestoreVersionEntryAsync(entry, request, progress, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogWarning("Skipped unknown backup entry {Entry}", entry.FullName);
        return Result.Success();
    }

    private async Task<Result> RestoreDataEntryAsync(
        ZipArchiveEntry entry,
        BackupRestoreRequest request,
        BackupRestoreProgress progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DataPath))
        {
            return Result.Failure("Vintage Story data path is not configured.");
        }

        var relative = entry.FullName["data/".Length..];
        if (!TryResolveSafeDestination(request.DataPath, relative, out var destination))
        {
            _logger.LogWarning("Skipped unsafe backup entry {Entry}", entry.FullName);
            return Result.Success();
        }

        await ExtractEntryAsync(entry, destination, cancellationToken).ConfigureAwait(false);
        progress.FileCount++;
        if (relative.StartsWith("Mods/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("ModConfig/", StringComparison.OrdinalIgnoreCase))
        {
            progress.RestoredMods = true;
        }
        else if (relative.StartsWith("Saves/", StringComparison.OrdinalIgnoreCase) ||
                 relative.StartsWith("BackupSaves/", StringComparison.OrdinalIgnoreCase))
        {
            progress.RestoredWorlds = true;
        }

        return Result.Success();
    }

    private async Task<Result> RestoreVersionEntryAsync(
        ZipArchiveEntry entry,
        BackupRestoreRequest request,
        BackupRestoreProgress progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InstallsRoot))
        {
            return Result.Failure("Installs root is not configured.");
        }

        var relative = entry.FullName["versions/".Length..];
        var separatorIndex = relative.IndexOf('/', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return Result.Success();
        }

        var version = relative[..separatorIndex];
        var versionRelative = relative[(separatorIndex + 1)..];
        var versionDir = GameInstallLayout.GetVersionDirectory(request.InstallsRoot, version);
        if (!TryResolveSafeDestination(versionDir, versionRelative, out var destination))
        {
            _logger.LogWarning("Skipped unsafe backup entry {Entry}", entry.FullName);
            return Result.Success();
        }

        await ExtractEntryAsync(entry, destination, cancellationToken).ConfigureAwait(false);
        progress.FileCount++;
        if (!progress.RestoredVersions.Contains(version, StringComparer.OrdinalIgnoreCase))
        {
            progress.RestoredVersions.Add(version);
        }

        return Result.Success();
    }

    private static async Task ExtractEntryAsync(ZipArchiveEntry entry, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var entryStream = entry.Open();
        await using (entryStream.ConfigureAwait(false))
        {
            var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            await using (output.ConfigureAwait(false))
            {
                await entryStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task UpdateInstalledVersionsAsync(string installsRoot, IReadOnlyList<string> versions, CancellationToken cancellationToken)
    {
        var existing = await _installedStore.ListAsync(installsRoot, cancellationToken).ConfigureAwait(false);
        var list = existing.IsSuccess
            ? existing.Value!.Where(v => !versions.Contains(v.Version, StringComparer.OrdinalIgnoreCase)).ToList()
            : [];

        foreach (var version in versions)
        {
            var versionDir = GameInstallLayout.GetVersionDirectory(installsRoot, version);
            var exe = VintageStoryExecutableLocator.FindClientExecutable(versionDir);
            list.Add(new InstalledGameVersion
            {
                Version = version,
                InstallPath = versionDir,
                ExecutablePath = exe,
                ExecutableFound = exe is not null,
                InstalledAt = DateTimeOffset.UtcNow,
            });
        }

        await _installedStore.SaveAsync(installsRoot, list, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<BackupManifest>> ReadManifestAsync(string zipPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            return Result<BackupManifest>.Failure("Backup file not found.");
        }

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.GetEntry("manifest.json");
            if (entry is null)
            {
                return Result<BackupManifest>.Failure("This file is not a Relic backup (missing manifest.json).");
            }

            var stream = entry.Open();
            await using (stream.ConfigureAwait(false))
            {
                var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
                return manifest is null
                    ? Result<BackupManifest>.Failure("Could not read backup manifest.")
                    : Result<BackupManifest>.Success(manifest);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            return Result<BackupManifest>.Failure("Could not read backup manifest: " + ex.Message);
        }
    }

    private static bool TryResolveSafeDestination(string rootDir, string relativeEntryPath, out string destination)
    {
        destination = string.Empty;
        if (string.IsNullOrWhiteSpace(relativeEntryPath))
        {
            return false;
        }

        var normalizedRoot = Path.GetFullPath(rootDir);
        var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, relativeEntryPath));
        var withSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(withSeparator, StringComparison.Ordinal))
        {
            return false;
        }

        destination = candidate;
        return true;
    }

    private sealed class BackupRestoreProgress
    {
        public bool RestoredMods { get; set; }
        public bool RestoredWorlds { get; set; }
        public List<string> RestoredVersions { get; } = [];
        public int FileCount { get; set; }
    }
}
