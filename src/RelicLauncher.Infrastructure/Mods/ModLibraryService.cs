using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Versions;
using RelicLauncher.Infrastructure.IO;

namespace RelicLauncher.Infrastructure.Mods;

public sealed class ModLibraryService : IModLibraryService
{
    private const string DisabledSuffix = RelicDefaults.DisabledModSuffix;
    private const string IndexFileName = "index.json";
    private readonly HttpClient _httpClient;
    private readonly IAppPathProvider _pathProvider;
    private readonly ILogger<ModLibraryService> _logger;

    public ModLibraryService(IAppPathProvider pathProvider, ILogger<ModLibraryService> logger)
        : this(pathProvider, logger, CreateDefaultClient())
    {
    }

    internal ModLibraryService(IAppPathProvider pathProvider, ILogger<ModLibraryService> logger, HttpClient httpClient)
    {
        _pathProvider = pathProvider;
        _logger = logger;
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RelicLauncher", "0.1.0"));
        }
    }

    public Task<Result<IReadOnlyList<LocalModInfo>>> ListInstalledAsync(string dataPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var modsDir = GameInstallLayout.GetModsDirectory(dataPath);
            Directory.CreateDirectory(modsDir);
            var list = new List<LocalModInfo>();

            foreach (var path in Directory.EnumerateFileSystemEntries(modsDir))
            {
                var name = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.'))
                {
                    continue;
                }

                var info = ReadLocalMod(path);
                if (info is not null)
                {
                    list.Add(info);
                }
            }

            return Task.FromResult(Result<IReadOnlyList<LocalModInfo>>.Success(list));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Result<IReadOnlyList<LocalModInfo>>.Failure(ex.Message));
        }
    }

    public async Task<Result<LocalModInfo>> InstallAsync(
        string dataPath,
        ModReleaseInfo release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (release.FileId <= 0)
        {
            return Result<LocalModInfo>.Failure("Release has no file id.");
        }

        if (string.IsNullOrWhiteSpace(release.DownloadUrl))
        {
            return Result<LocalModInfo>.Failure("Release has no download URL.");
        }

        try
        {
            var modsDir = GameInstallLayout.GetModsDirectory(dataPath);
            Directory.CreateDirectory(modsDir);
            var fileName = string.IsNullOrWhiteSpace(release.FileName)
                ? $"mod_{release.FileId}.zip"
                : Path.GetFileName(release.FileName);
            var destination = Path.Combine(modsDir, fileName);

            var cachePath = await EnsureCachedAsync(release, progress, cancellationToken).ConfigureAwait(false);
            if (!cachePath.IsSuccess)
            {
                return Result<LocalModInfo>.Failure(cachePath.Error ?? "Mod download failed.");
            }

            File.Copy(cachePath.Value!, destination, overwrite: true);

            var info = ReadLocalMod(destination);
            if (info is null)
            {
                return Result<LocalModInfo>.Failure("Installed file could not be read.");
            }

            UpdateIndex(release.FileId, fileName, info.ModId);
            await RemoveOtherReleasesAsync(dataPath, info).ConfigureAwait(false);

            var refreshed = ReadLocalMod(destination);
            return refreshed is null
                ? Result<LocalModInfo>.Failure("Installed file could not be read.")
                : Result<LocalModInfo>.Success(refreshed);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Mod install failed for file {FileId}", release.FileId);
            return Result<LocalModInfo>.Failure(ex.Message);
        }
    }

    public Task<Result> UninstallAsync(LocalModInfo mod, CancellationToken cancellationToken = default)
    {
        try
        {
            if (mod.IsDirectory)
            {
                if (Directory.Exists(mod.Path))
                {
                    Directory.Delete(mod.Path, recursive: true);
                }
            }
            else if (File.Exists(mod.Path))
            {
                File.Delete(mod.Path);
            }

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Result.Failure(ex.Message));
        }
    }

    public Task<Result<LocalModInfo>> SetEnabledAsync(LocalModInfo mod, bool enabled, CancellationToken cancellationToken = default)
    {
        try
        {
            if (mod.IsEnabled == enabled)
            {
                return Task.FromResult(Result<LocalModInfo>.Success(mod));
            }

            string newPath;
            if (enabled)
            {
                if (!mod.Path.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(Result<LocalModInfo>.Success(mod));
                }

                newPath = mod.Path[..^DisabledSuffix.Length];
            }
            else
            {
                if (mod.Path.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(Result<LocalModInfo>.Success(mod));
                }

                newPath = mod.Path + DisabledSuffix;
            }

            if (mod.IsDirectory)
            {
                Directory.Move(mod.Path, newPath);
            }
            else
            {
                File.Move(mod.Path, newPath, overwrite: false);
            }

            var updated = ReadLocalMod(newPath);
            return Task.FromResult(updated is null
                ? Result<LocalModInfo>.Failure("Could not read mod after enable/disable.")
                : Result<LocalModInfo>.Success(updated));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Result<LocalModInfo>.Failure(ex.Message));
        }
    }

    public async Task<Result<int>> CleanDuplicateModsAsync(string dataPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var listed = await ListInstalledAsync(dataPath, cancellationToken).ConfigureAwait(false);
            if (!listed.IsSuccess)
            {
                return Result<int>.Failure(listed.Error ?? "Could not list installed mods.");
            }

            var removed = 0;
            var groups = listed.Value!
                .Where(m => !string.IsNullOrWhiteSpace(m.ModId))
                .GroupBy(m => m.ModId!, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (var group in groups)
            {
                var keep = group
                    .OrderByDescending(m => m.IsEnabled)
                    .ThenByDescending(m => m.Version ?? string.Empty, Comparer<string>.Create(GameVersionComparer.Compare))
                    .ThenBy(m => m.FileName, StringComparer.OrdinalIgnoreCase)
                    .First();

                foreach (var duplicate in group.Where(m =>
                             !string.Equals(m.Path, keep.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    var result = await UninstallAsync(duplicate, cancellationToken).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        removed++;
                    }
                }
            }

            return Result<int>.Success(removed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result<int>.Failure(ex.Message);
        }
    }

    public async Task<Result<LocalModInfo>> ImportLocalAsync(
        string dataPath,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return Result<LocalModInfo>.Failure("Source path is required.");
        }

        try
        {
            var modsDir = GameInstallLayout.GetModsDirectory(dataPath);
            Directory.CreateDirectory(modsDir);

            var destinationResult = Directory.Exists(sourcePath)
                ? ImportFolder(sourcePath, modsDir)
                : File.Exists(sourcePath)
                    ? ImportZip(sourcePath, modsDir)
                    : Result<string>.Failure("Source path does not exist.");
            if (!destinationResult.IsSuccess)
            {
                return Result<LocalModInfo>.Failure(destinationResult.Error ?? "Import failed.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var info = ReadLocalMod(destinationResult.Value!);
            if (info is null)
            {
                return Result<LocalModInfo>.Failure("Imported path could not be read as a mod.");
            }

            await RemoveOtherReleasesAsync(dataPath, info).ConfigureAwait(false);
            var refreshed = ReadLocalMod(destinationResult.Value!);
            return refreshed is null
                ? Result<LocalModInfo>.Failure("Imported path could not be read as a mod.")
                : Result<LocalModInfo>.Success(refreshed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Local mod import failed from {Path}", sourcePath);
            return Result<LocalModInfo>.Failure(ex.Message);
        }
    }

    private static Result<string> ImportFolder(string sourcePath, string modsDir)
    {
        var modInfoPath = Path.Combine(sourcePath, "modinfo.json");
        if (!File.Exists(modInfoPath))
        {
            return Result<string>.Failure("Folder must contain modinfo.json at its root.");
        }

        var folderName = Path.GetFileName(
            Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return Result<string>.Failure("Could not determine folder name.");
        }

        var destination = Path.Combine(modsDir, folderName);
        if (Directory.Exists(destination) || File.Exists(destination))
        {
            return Result<string>.Failure($"A mod named {folderName} already exists in Mods.");
        }

        CopyDirectory(sourcePath, destination);
        return Result<string>.Success(destination);
    }

    private static Result<string> ImportZip(string sourcePath, string modsDir)
    {
        if (!sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Failure("Only .zip archives or mod folders are supported.");
        }

        var fileName = Path.GetFileName(sourcePath);
        var destination = Path.Combine(modsDir, fileName);
        File.Copy(sourcePath, destination, overwrite: true);
        return Result<string>.Success(destination);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var dest = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDir))
        {
            var name = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
            {
                continue;
            }

            CopyDirectory(directory, Path.Combine(destinationDir, name));
        }
    }

    internal static LocalModInfo? ReadLocalMod(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
        {
            return null;
        }

        var isDir = Directory.Exists(path);
        var enabled = !name.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase);
        string? modId = null;
        string? displayName = null;
        string? version = null;

        try
        {
            var modInfoJson = TryReadModInfoJson(path, isDir);
            if (modInfoJson is not null)
            {
                using var doc = JsonDocument.Parse(modInfoJson);
                var root = doc.RootElement;
                modId = root.TryGetProperty("modid", out var id) ? id.GetString() : null;
                displayName = root.TryGetProperty("name", out var n) ? n.GetString() : null;
                version = root.TryGetProperty("version", out var v) ? v.GetString() : null;
            }
        }
        catch (JsonException)
        {
        }

        return new LocalModInfo
        {
            Path = path,
            FileName = name,
            ModId = modId,
            Name = displayName ?? StripDisabled(name),
            Version = version,
            IsEnabled = enabled,
            IsDirectory = isDir,
        };
    }

    private async Task<Result<string>> EnsureCachedAsync(
        ModReleaseInfo release,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var cacheDir = GetFileCacheDirectory();
        Directory.CreateDirectory(cacheDir);
        var cachePath = Path.Combine(cacheDir, $"{release.FileId}.zip");
        if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
        {
            progress?.Report(1);
            return Result<string>.Success(cachePath);
        }

        var download = await DownloadToCacheAsync(release, cachePath, progress, cancellationToken)
            .ConfigureAwait(false);
        return download.IsSuccess
            ? Result<string>.Success(cachePath)
            : Result<string>.Failure(download.Error ?? "Mod download failed.");
    }

    private async Task<Result> DownloadToCacheAsync(
        ModReleaseInfo release,
        string cachePath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var tempPath = cachePath + ".partial";
        try
        {
            using var response = await _httpClient.GetAsync(
                release.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure($"Download failed with status {(int)response.StatusCode}.");
            }

            var total = response.Content.Headers.ContentLength;
            using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var copy = await BoundedStreamCopy.CopyAsync(
                input,
                output,
                total,
                RelicDefaults.MaxModDownloadBytes,
                progress,
                cancellationToken).ConfigureAwait(false);
            if (!copy.IsSuccess)
            {
                return Result.Failure(copy.Error ?? "Download exceeded size limit.");
            }

            File.Move(tempPath, cachePath, overwrite: true);
            return Result.Success();
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private async Task RemoveOtherReleasesAsync(string dataPath, LocalModInfo installed)
    {
        if (string.IsNullOrWhiteSpace(installed.ModId))
        {
            return;
        }

        var listed = await ListInstalledAsync(dataPath).ConfigureAwait(false);
        if (!listed.IsSuccess)
        {
            return;
        }

        foreach (var other in listed.Value!)
        {
            if (string.Equals(other.Path, installed.Path, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(other.ModId, installed.ModId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var removed = await UninstallAsync(other).ConfigureAwait(false);
            if (!removed.IsSuccess)
            {
                _logger.LogWarning(
                    "Could not remove duplicate mod {File} for modid {ModId}: {Error}",
                    other.FileName,
                    installed.ModId,
                    removed.Error);
            }
        }
    }

    private void UpdateIndex(int fileId, string fileName, string? modId)
    {
        try
        {
            var cacheDir = GetFileCacheDirectory();
            Directory.CreateDirectory(cacheDir);
            var indexPath = Path.Combine(cacheDir, IndexFileName);
            var map = LoadIndex(indexPath);
            map[fileId.ToString(CultureInfo.InvariantCulture)] = new ModFileIndexEntry
            {
                FileId = fileId,
                FileName = fileName,
                ModId = modId,
            };
            var json = JsonSerializer.Serialize(map, IndexJsonOptions);
            File.WriteAllText(indexPath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogDebug(ex, "Could not update mod file index for {FileId}", fileId);
        }
    }

    private static Dictionary<string, ModFileIndexEntry> LoadIndex(string indexPath)
    {
        if (!File.Exists(indexPath))
        {
            return new Dictionary<string, ModFileIndexEntry>(StringComparer.Ordinal);
        }

        try
        {
            var json = File.ReadAllText(indexPath);
            return JsonSerializer.Deserialize<Dictionary<string, ModFileIndexEntry>>(json, IndexJsonOptions)
                   ?? new Dictionary<string, ModFileIndexEntry>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, ModFileIndexEntry>(StringComparer.Ordinal);
        }
    }

    private string GetFileCacheDirectory()
        => Path.Combine(_pathProvider.GetPaths().CacheDirectory, "mods", "files");

    private static string? TryReadModInfoJson(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            var file = Path.Combine(path, "modinfo.json");
            return File.Exists(file) ? File.ReadAllText(file) : null;
        }

        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith(".zip" + DisabledSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using var archive = ZipFile.OpenRead(path);
        var entry = archive.Entries.FirstOrDefault(e =>
            string.Equals(Path.GetFileName(e.FullName), "modinfo.json", StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string StripDisabled(string name)
        => name.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase)
            ? name[..^DisabledSuffix.Length]
            : name;

    private static HttpClient CreateDefaultClient()
        => new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(60),
        };

    private static readonly JsonSerializerOptions IndexJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private sealed class ModFileIndexEntry
    {
        public int FileId { get; set; }
        public string? FileName { get; set; }
        public string? ModId { get; set; }
    }
}
