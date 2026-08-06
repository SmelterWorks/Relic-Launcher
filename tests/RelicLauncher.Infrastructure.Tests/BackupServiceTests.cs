using System.IO.Compression;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Backup;
using RelicLauncher.Infrastructure.Versions;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class BackupServiceTests
{
    [Fact]
    public async Task CreateAsync_Fails_WhenNothingSelected()
    {
        using var temp = new TempAppPaths();
        var service = new BackupService(new JsonInstalledVersionStore(), NullLogger<BackupService>.Instance);

        var result = await service.CreateAsync(new BackupRequest
        {
            DestinationZipPath = Path.Combine(temp.Paths.RootDirectory, "backup.zip"),
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Select at least one");
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenVersionNotInstalled()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var service = new BackupService(new JsonInstalledVersionStore(), NullLogger<BackupService>.Instance);

        var result = await service.CreateAsync(new BackupRequest
        {
            DestinationZipPath = Path.Combine(temp.Paths.RootDirectory, "backup.zip"),
            InstallsRoot = installsRoot,
            VersionsToInclude = ["1.22.6"],
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("1.22.6");
    }

    [Fact]
    public async Task CreateAsync_ModsOnly_WritesManifestAndModEntries()
    {
        using var temp = new TempAppPaths();
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        Directory.CreateDirectory(Path.Combine(dataPath, "Mods"));
        await File.WriteAllTextAsync(Path.Combine(dataPath, "Mods", "example.zip"), "mod-bytes");
        var zipPath = Path.Combine(temp.Paths.RootDirectory, "backup.zip");
        var service = new BackupService(new JsonInstalledVersionStore(), NullLogger<BackupService>.Instance);

        var result = await service.CreateAsync(new BackupRequest
        {
            DestinationZipPath = zipPath,
            DataPath = dataPath,
            IncludeMods = true,
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.IncludedMods.Should().BeTrue();
        result.Value.FileCount.Should().Be(1);
        File.Exists(zipPath).Should().BeTrue();

        using var archive = ZipFile.OpenRead(zipPath);
        archive.GetEntry("manifest.json").Should().NotBeNull();
        archive.GetEntry("data/Mods/example.zip").Should().NotBeNull();
        archive.GetEntry("data/Saves").Should().BeNull();
    }

    [Fact]
    public async Task RoundTrip_CreateThenRestore_RestoresModsWorldsAndVersion_AndUpdatesInventory()
    {
        using var temp = new TempAppPaths();
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        Directory.CreateDirectory(Path.Combine(dataPath, "Mods"));
        Directory.CreateDirectory(Path.Combine(dataPath, "Saves"));
        await File.WriteAllTextAsync(Path.Combine(dataPath, "Mods", "example.zip"), "mod-bytes");
        await File.WriteAllTextAsync(Path.Combine(dataPath, "Saves", "world.vcdbs"), "world-bytes");

        var versionDir = Path.Combine(installsRoot, "versions", "1.22.6");
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(Path.Combine(versionDir, "Vintagestory"), "bin");

        var installedStore = new JsonInstalledVersionStore();
        var service = new BackupService(installedStore, NullLogger<BackupService>.Instance);
        var zipPath = Path.Combine(temp.Paths.RootDirectory, "backup.zip");

        var create = await service.CreateAsync(new BackupRequest
        {
            DestinationZipPath = zipPath,
            DataPath = dataPath,
            InstallsRoot = installsRoot,
            IncludeMods = true,
            IncludeWorlds = true,
            VersionsToInclude = ["1.22.6"],
        });
        create.IsSuccess.Should().BeTrue();

        var manifest = await service.ReadManifestAsync(zipPath);
        manifest.IsSuccess.Should().BeTrue();
        manifest.Value!.IncludesMods.Should().BeTrue();
        manifest.Value.IncludesWorlds.Should().BeTrue();
        manifest.Value.Versions.Should().Contain("1.22.6");

        var restoreDataPath = Path.Combine(temp.Paths.RootDirectory, "restored-data");
        var restoreInstallsRoot = Path.Combine(temp.Paths.RootDirectory, "restored-installs");
        var restore = await service.RestoreAsync(new BackupRestoreRequest
        {
            SourceZipPath = zipPath,
            DataPath = restoreDataPath,
            InstallsRoot = restoreInstallsRoot,
        });

        restore.IsSuccess.Should().BeTrue();
        restore.Value!.RestoredMods.Should().BeTrue();
        restore.Value.RestoredWorlds.Should().BeTrue();
        restore.Value.RestoredVersions.Should().Contain("1.22.6");

        File.Exists(Path.Combine(restoreDataPath, "Mods", "example.zip")).Should().BeTrue();
        File.Exists(Path.Combine(restoreDataPath, "Saves", "world.vcdbs")).Should().BeTrue();
        var restoredVersionExe = Path.Combine(restoreInstallsRoot, "versions", "1.22.6", "Vintagestory");
        File.Exists(restoredVersionExe).Should().BeTrue();

        var inventory = await installedStore.ListAsync(restoreInstallsRoot);
        inventory.IsSuccess.Should().BeTrue();
        var entry = inventory.Value!.Should().ContainSingle(v => v.Version == "1.22.6").Subject;
        entry.ExecutableFound.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreAsync_SkipsZipSlipEntries()
    {
        using var temp = new TempAppPaths();
        var zipPath = Path.Combine(temp.Paths.RootDirectory, "malicious.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var traversal = archive.CreateEntry("data/../../evil.txt");
            await using (var stream = traversal.Open())
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync("pwned");
            }

            var safe = archive.CreateEntry("data/Mods/good.zip");
            await using (var stream = safe.Open())
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync("mod-bytes");
            }
        }

        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        var service = new BackupService(new JsonInstalledVersionStore(), NullLogger<BackupService>.Instance);

        var result = await service.RestoreAsync(new BackupRestoreRequest
        {
            SourceZipPath = zipPath,
            DataPath = dataPath,
        });

        result.IsSuccess.Should().BeTrue();
        File.Exists(Path.Combine(dataPath, "Mods", "good.zip")).Should().BeTrue();
        File.Exists(Path.Combine(temp.Paths.RootDirectory, "evil.txt")).Should().BeFalse();
    }

    [Fact]
    public async Task ReadManifestAsync_Fails_WhenNotARelicBackup()
    {
        using var temp = new TempAppPaths();
        var zipPath = Path.Combine(temp.Paths.RootDirectory, "plain.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("readme.txt");
            await using var stream = entry.Open();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("not a backup");
        }

        var service = new BackupService(new JsonInstalledVersionStore(), NullLogger<BackupService>.Instance);
        var result = await service.ReadManifestAsync(zipPath);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("manifest.json");
    }
}
