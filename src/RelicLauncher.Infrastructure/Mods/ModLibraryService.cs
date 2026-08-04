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

namespace RelicLauncher.Infrastructure.Mods;

public sealed class ModLibraryService : IModLibraryService
{
    private const string DisabledSuffix = RelicDefaults.DisabledModSuffix;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ModLibraryService> _logger;

    public ModLibraryService(ILogger<ModLibraryService> logger)
        : this(logger, CreateDefaultClient())
    {
    }

    internal ModLibraryService(ILogger<ModLibraryService> logger, HttpClient httpClient)
    {
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

            using var response = await _httpClient.GetAsync(
                release.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<LocalModInfo>.Failure($"Download failed with status {(int)response.StatusCode}.");
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
                    progress?.Report(Math.Clamp(readTotal / (double)total.Value, 0, 0.99));
                }
                else
                {
                    progress?.Report(0.5);
                }
            }

            progress?.Report(1.0);

            var info = ReadLocalMod(destination);
            return info is null
                ? Result<LocalModInfo>.Failure("Installed file could not be read.")
                : Result<LocalModInfo>.Success(info);
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
            // Keep filename-based metadata.
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
}
