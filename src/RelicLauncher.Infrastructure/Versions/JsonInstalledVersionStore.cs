using System.Text.Json;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Versions;

public sealed class JsonInstalledVersionStore : IInstalledVersionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<Result<IReadOnlyList<InstalledGameVersion>>> ListAsync(string installsRoot, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(installsRoot))
            {
                return Result<IReadOnlyList<InstalledGameVersion>>.Success([]);
            }

            var path = GameInstallLayout.GetInventoryPath(installsRoot);
            if (!File.Exists(path))
            {
                var scanned = ScanVersionsDirectory(installsRoot);
                return Result<IReadOnlyList<InstalledGameVersion>>.Success(scanned);
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var versions = JsonSerializer.Deserialize<List<InstalledGameVersion>>(json, JsonOptions) ?? [];
            return Result<IReadOnlyList<InstalledGameVersion>>.Success(versions);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Result<IReadOnlyList<InstalledGameVersion>>.Failure(ex.Message);
        }
    }

    public async Task<Result> SaveAsync(string installsRoot, IReadOnlyList<InstalledGameVersion> versions, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(installsRoot);
            var path = GameInstallLayout.GetInventoryPath(installsRoot);
            var json = JsonSerializer.Serialize(versions, JsonOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static IReadOnlyList<InstalledGameVersion> ScanVersionsDirectory(string installsRoot)
    {
        var root = GameInstallLayout.GetVersionsRoot(installsRoot);
        if (!Directory.Exists(root))
        {
            return [];
        }

        var list = new List<InstalledGameVersion>();
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var version = Path.GetFileName(dir);
            var exe = RelicLauncher.Core.Paths.VintageStoryExecutableLocator.FindClientExecutable(dir);
            list.Add(new InstalledGameVersion
            {
                Version = version,
                InstallPath = dir,
                ExecutablePath = exe,
                ExecutableFound = exe is not null,
                InstalledAt = new DateTimeOffset(Directory.GetCreationTimeUtc(dir), TimeSpan.Zero),
            });
        }

        return list;
    }
}
