using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Modpacks;

public sealed partial class ModpackService : IModpackService
{
    private const string ManifestFileName = "manifest.json";
    private const string ModpackFormat = "relic-modpack";
    private const string ModsArchivePrefix = "mods/";

    private readonly IAppPathProvider _pathProvider;
    private readonly IModLibraryService _modLibrary;
    private readonly IModOriginResolver _originResolver;
    private readonly IModReleaseResolver _releaseResolver;
    private readonly IModDependencyInstallPlanner _dependencyPlanner;
    private readonly ILogger<ModpackService> _logger;

    public ModpackService(
        IAppPathProvider pathProvider,
        IModLibraryService modLibrary,
        IModOriginResolver originResolver,
        IModReleaseResolver releaseResolver,
        IModDependencyInstallPlanner dependencyPlanner,
        ILogger<ModpackService> logger)
    {
        _pathProvider = pathProvider;
        _modLibrary = modLibrary;
        _originResolver = originResolver;
        _releaseResolver = releaseResolver;
        _dependencyPlanner = dependencyPlanner;
        _logger = logger;
    }

    public async Task<Result<ModpackManifest>> ReadManifestAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result<ModpackManifest>.Failure("Path is required.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(path))
            {
                return await ReadManifestFromDirectoryAsync(path, cancellationToken).ConfigureAwait(false);
            }

            if (!File.Exists(path))
            {
                return Result<ModpackManifest>.Failure("Modpack file not found.");
            }

            return await ReadManifestFromZipAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Could not read modpack manifest from {Path}", path);
            return Result<ModpackManifest>.Failure("Could not read modpack: " + ex.Message);
        }
    }

    private async Task<Result<ModpackManifest>> ReadManifestFromDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(path, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return Result<ModpackManifest>.Failure("Manifest not found in modpack directory.");
        }

        var json = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        return Result<ModpackManifest>.Success(ValidateManifest(ModpackManifestCodec.Deserialize(json)));
    }

    private static async Task<Result<ModpackManifest>> ReadManifestFromZipAsync(string path, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry(ManifestFileName)
                    ?? archive.Entries.FirstOrDefault(e =>
                        string.Equals(e.FullName, ManifestFileName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return Result<ModpackManifest>.Failure("Modpack manifest not found.");
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var manifestJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return Result<ModpackManifest>.Success(ValidateManifest(ModpackManifestCodec.Deserialize(manifestJson)));
    }

    private string GetLocalModpacksRoot()
        => Path.Combine(_pathProvider.GetPaths().RootDirectory, "modpacks");

    private static ModpackManifest ValidateManifest(ModpackManifest manifest)
    {
        if (!string.Equals(manifest.Format, ModpackFormat, StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException("Unsupported modpack format.");
        }

        if (manifest.SchemaVersion > 1)
        {
            throw new JsonException("Unsupported modpack schema version.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            throw new JsonException("Modpack name is required.");
        }

        return manifest;
    }

    private static bool VersionsMatch(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void ReportProgress(IProgress<double>? progress, double value)
        => progress?.Report(Math.Clamp(value, 0, 1));

    private static void WriteManifestEntry(ZipArchive archive, ModpackManifest manifest)
    {
        var manifestEntry = archive.CreateEntry(ManifestFileName, CompressionLevel.Optimal);
        using var stream = manifestEntry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(ModpackManifestCodec.Serialize(manifest));
    }

    private static void WriteManifestJsonEntry(ZipArchive archive, string json)
    {
        var manifestEntry = archive.CreateEntry(ManifestFileName, CompressionLevel.Optimal);
        using var stream = manifestEntry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(json);
    }

    private static string SanitizeArchiveName(string value)
    {
        var builder = new StringBuilder(value.Trim().Length);
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '.' or '_')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('_');
            }
        }

        var trimmed = builder.ToString();
        return string.IsNullOrWhiteSpace(trimmed) ? "mod" : trimmed;
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }
}
