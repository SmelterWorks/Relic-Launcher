using System.Text.Json;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Versions;

namespace RelicLauncher.Infrastructure.Server;

public sealed class JsonInstalledServerStore : IInstalledServerStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<Result<IReadOnlyList<InstalledServerVersion>>> ListAsync(string installsRoot, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(installsRoot))
            {
                return Result<IReadOnlyList<InstalledServerVersion>>.Success([]);
            }

            var path = GameServerInstallLayout.GetInventoryPath(installsRoot);
            if (!File.Exists(path))
            {
                var scanned = ScanServersDirectory(installsRoot);
                return Result<IReadOnlyList<InstalledServerVersion>>.Success(scanned);
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var versions = JsonSerializer.Deserialize<List<InstalledServerVersion>>(json, JsonOptions) ?? [];
            var merged = MergeWithDisk(installsRoot, versions);
            return Result<IReadOnlyList<InstalledServerVersion>>.Success(merged);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Result<IReadOnlyList<InstalledServerVersion>>.Failure(ex.Message);
        }
    }

    public async Task<Result> SaveAsync(string installsRoot, IReadOnlyList<InstalledServerVersion> versions, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(installsRoot);
            var path = GameServerInstallLayout.GetInventoryPath(installsRoot);
            var json = JsonSerializer.Serialize(versions, JsonOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure(ex.Message);
        }
    }

    internal static IReadOnlyList<InstalledServerVersion> MergeWithDisk(
        string installsRoot,
        IReadOnlyList<InstalledServerVersion> stored)
    {
        var scanned = ScanServersDirectory(installsRoot);
        if (scanned.Count == 0)
        {
            return stored
                .OrderByDescending(v => v.Version, Comparer<string>.Create(GameVersionComparer.Compare))
                .ToArray();
        }

        var merged = new Dictionary<string, InstalledServerVersion>(StringComparer.OrdinalIgnoreCase);
        foreach (var version in stored)
        {
            if (!string.IsNullOrWhiteSpace(version.Version))
            {
                merged[version.Version] = version;
            }
        }

        foreach (var version in scanned)
        {
            if (!merged.TryGetValue(version.Version, out var existing))
            {
                merged[version.Version] = version;
                continue;
            }

            if (!existing.ExecutableFound && version.ExecutableFound)
            {
                merged[version.Version] = new InstalledServerVersion
                {
                    Version = version.Version,
                    InstallPath = version.InstallPath,
                    ExecutablePath = version.ExecutablePath,
                    ExecutableFound = version.ExecutableFound,
                    InstalledAt = existing.InstalledAt,
                };
            }
        }

        return merged.Values
            .OrderByDescending(v => v.Version, Comparer<string>.Create(GameVersionComparer.Compare))
            .ToArray();
    }

    private static IReadOnlyList<InstalledServerVersion> ScanServersDirectory(string installsRoot)
    {
        var root = GameServerInstallLayout.GetServersRoot(installsRoot);
        if (!Directory.Exists(root))
        {
            return [];
        }

        var list = new List<InstalledServerVersion>();
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var version = Path.GetFileName(dir);
            var exe = VintageStoryServerExecutableLocator.FindServerExecutable(dir);
            list.Add(new InstalledServerVersion
            {
                Version = version,
                InstallPath = dir,
                ExecutablePath = exe,
                ExecutableFound = exe is not null,
                InstalledAt = new DateTimeOffset(Directory.GetCreationTimeUtc(dir), TimeSpan.Zero),
            });
        }

        return list
            .OrderByDescending(v => v.Version, Comparer<string>.Create(GameVersionComparer.Compare))
            .ToArray();
    }
}
