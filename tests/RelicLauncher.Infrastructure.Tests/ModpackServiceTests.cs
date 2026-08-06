using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Modpacks;
using RelicLauncher.Infrastructure.Mods;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModpackServiceTests
{
    [Fact]
    public async Task ExportAsync_OnlinePack_WritesManifestWithoutEmbeddedMods()
    {
        using var temp = new TempAppPaths();
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        var modsDir = Path.Combine(dataPath, "Mods");
        Directory.CreateDirectory(modsDir);
        var zipBytes = CreateZip("carrycapacity", "Carry", "1.0.0", "{}");
        await File.WriteAllBytesAsync(Path.Combine(modsDir, "mod_10.zip"), zipBytes);

        var indexPath = Path.Combine(temp.Paths.CacheDirectory, "mods", "files", "index.json");
        Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
        await File.WriteAllTextAsync(indexPath, """{"10":{"fileId":10,"fileName":"mod_10.zip","modId":"carrycapacity"}}""");

        var library = CreateLibrary(temp, new Dictionary<int, byte[]> { [10] = zipBytes });
        var service = CreateService(temp, library);

        var mod = new LocalModInfo
        {
            Path = Path.Combine(modsDir, "mod_10.zip"),
            FileName = "mod_10.zip",
            ModId = "carrycapacity",
            Version = "1.0.0",
            IsEnabled = true,
        };

        var destination = Path.Combine(temp.Paths.RootDirectory, "pack.relicmodpack");
        var result = await service.ExportAsync(new ModpackExportRequest
        {
            DestinationPath = destination,
            DataPath = dataPath,
            GameVersion = "1.22.0",
            Name = "Test Pack",
            Mods = [mod],
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Manifest.Distribution.Should().Be(ModpackDistribution.Online);
        using var archive = ZipFile.OpenRead(destination);
        archive.Entries.Should().ContainSingle(e => e.FullName == "manifest.json");
    }

    [Fact]
    public async Task ExportAsync_LocalFolder_CreatesOfflinePackWithEmbeddedMod()
    {
        using var temp = new TempAppPaths();
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        var modsDir = Path.Combine(dataPath, "Mods");
        var modDir = Path.Combine(modsDir, "mydevmod");
        Directory.CreateDirectory(modDir);
        await File.WriteAllTextAsync(
            Path.Combine(modDir, "modinfo.json"),
            """{"modid":"mydevmod","name":"Dev","version":"0.1.0","dependencies":{}}""");

        var library = CreateLibrary(temp, new Dictionary<int, byte[]>());
        var service = CreateService(temp, library);
        var mod = new LocalModInfo
        {
            Path = modDir,
            FileName = "mydevmod",
            ModId = "mydevmod",
            Version = "0.1.0",
            IsDirectory = true,
            IsEnabled = true,
        };

        var destination = Path.Combine(temp.Paths.RootDirectory, "offline.relicmodpack");
        var result = await service.ExportAsync(new ModpackExportRequest
        {
            DestinationPath = destination,
            DataPath = dataPath,
            GameVersion = "1.22.0",
            Name = "Offline Pack",
            Mods = [mod],
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Manifest.Distribution.Should().Be(ModpackDistribution.Offline);
        using var archive = ZipFile.OpenRead(destination);
        archive.Entries.Should().Contain(e => e.FullName.StartsWith("mods/", StringComparison.Ordinal));
        archive.GetEntry("manifest.json").Should().NotBeNull();
    }

    [Fact]
    public async Task ApplyAsync_OfflineMerge_ImportsEmbeddedMod()
    {
        using var temp = new TempAppPaths();
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        Directory.CreateDirectory(Path.Combine(dataPath, "Mods"));

        var packPath = Path.Combine(temp.Paths.RootDirectory, "apply.relicmodpack");
        var zipBytes = CreateZip("packedmod", "Packed", "1.0.0", "{}");
        using (var archive = ZipFile.Open(packPath, ZipArchiveMode.Create))
        {
            var modEntry = archive.CreateEntry("mods/packedmod.zip");
            await using (var stream = modEntry.Open())
            {
                await stream.WriteAsync(zipBytes);
            }

            var manifest = new ModpackManifest
            {
                Name = "Apply Pack",
                GameVersion = "1.22.0",
                CreatedAt = DateTimeOffset.UtcNow,
                Distribution = ModpackDistribution.Offline,
                Mods =
                [
                    new ModpackModEntry
                    {
                        ModId = "packedmod",
                        ModVersion = "1.0.0",
                        Source = ModpackModSource.Local,
                        ArchivePath = "mods/packedmod.zip",
                    },
                ],
            };
            WriteManifestEntry(archive, manifest);
        }

        var library = CreateLibrary(temp, new Dictionary<int, byte[]>());
        var service = CreateService(temp, library);
        var apply = await service.ApplyAsync(new ModpackApplyRequest
        {
            DataPath = dataPath,
            Manifest = (await service.ReadManifestAsync(packPath)).Value!,
            ZipPath = packPath,
            Mode = ModpackApplyMode.Merge,
        });

        apply.IsSuccess.Should().BeTrue();
        apply.Value!.InstalledCount.Should().Be(1);
        var installed = await library.ListInstalledAsync(dataPath);
        installed.Value!.Should().ContainSingle(m => m.ModId == "packedmod");
    }

    [Fact]
    public async Task ComputeApplyDiff_Replace_IncludesRemovals()
    {
        using var temp = new TempAppPaths();
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        var modsDir = Path.Combine(dataPath, "Mods");
        Directory.CreateDirectory(modsDir);
        await File.WriteAllBytesAsync(Path.Combine(modsDir, "extra.zip"), CreateZip("extra", "Extra", "1.0.0", "{}"));

        var library = CreateLibrary(temp, new Dictionary<int, byte[]>());
        var service = CreateService(temp, library);
        var manifest = new ModpackManifest
        {
            Name = "Diff Pack",
            GameVersion = "1.22.0",
            CreatedAt = DateTimeOffset.UtcNow,
            Mods =
            [
                new ModpackModEntry { ModId = "newmod", ModVersion = "1.0.0", FileId = 5, Source = ModpackModSource.ModDb },
            ],
        };

        var diff = await service.ComputeApplyDiffAsync(new ModpackApplyRequest
        {
            DataPath = dataPath,
            Manifest = manifest,
            Mode = ModpackApplyMode.Replace,
        });

        diff.IsSuccess.Should().BeTrue();
        diff.Value!.Entries.Should().Contain(e => e.Kind == ModpackApplyDiffKind.Add && e.ModId == "newmod");
        diff.Value.Entries.Should().Contain(e => e.Kind == ModpackApplyDiffKind.Remove && e.ModId == "extra");
    }

    [Fact]
    public async Task ComputeApplyDiff_Merge_DoesNotIncludeRemovals()
    {
        using var temp = new TempAppPaths();
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        var modsDir = Path.Combine(dataPath, "Mods");
        Directory.CreateDirectory(modsDir);
        await File.WriteAllBytesAsync(Path.Combine(modsDir, "extra.zip"), CreateZip("extra", "Extra", "1.0.0", "{}"));

        var library = CreateLibrary(temp, new Dictionary<int, byte[]>());
        var service = CreateService(temp, library);
        var manifest = new ModpackManifest
        {
            Name = "Merge Pack",
            GameVersion = "1.22.0",
            CreatedAt = DateTimeOffset.UtcNow,
            Mods =
            [
                new ModpackModEntry { ModId = "newmod", ModVersion = "1.0.0", FileId = 5, Source = ModpackModSource.ModDb },
            ],
        };

        var diff = await service.ComputeApplyDiffAsync(new ModpackApplyRequest
        {
            DataPath = dataPath,
            Manifest = manifest,
            Mode = ModpackApplyMode.Merge,
        });

        diff.IsSuccess.Should().BeTrue();
        diff.Value!.Entries.Should().NotContain(e => e.Kind == ModpackApplyDiffKind.Remove);
    }

    [Fact]
    public async Task ReadManifestAsync_RoundTrip_FromExport()
    {
        using var temp = new TempAppPaths();
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        var modsDir = Path.Combine(dataPath, "Mods");
        Directory.CreateDirectory(modsDir);
        var zipBytes = CreateZip("roundtrip", "Round", "1.0.0", "{}");
        await File.WriteAllBytesAsync(Path.Combine(modsDir, "mod_11.zip"), zipBytes);

        var library = CreateLibrary(temp, new Dictionary<int, byte[]> { [11] = zipBytes });
        var service = CreateService(temp, library);
        var destination = Path.Combine(temp.Paths.RootDirectory, "roundtrip.relicmodpack");

        await service.ExportAsync(new ModpackExportRequest
        {
            DestinationPath = destination,
            DataPath = dataPath,
            GameVersion = "1.22.0",
            Name = "Round Trip",
            Mods =
            [
                new LocalModInfo
                {
                    Path = Path.Combine(modsDir, "mod_11.zip"),
                    FileName = "mod_11.zip",
                    ModId = "roundtrip",
                    Version = "1.0.0",
                    IsEnabled = true,
                },
            ],
        });

        var read = await service.ReadManifestAsync(destination);
        read.IsSuccess.Should().BeTrue();
        read.Value!.Name.Should().Be("Round Trip");
        read.Value.Mods.Should().ContainSingle(m => m.ModId == "roundtrip");
    }

    private static ModpackService CreateService(TempAppPaths temp, ModLibraryService library)
    {
        var originResolver = new ModOriginResolver(new FixedPathProvider(temp.Paths));
        var resolver = new StubReleaseResolver(new Dictionary<string, ModReleaseInfo>(StringComparer.OrdinalIgnoreCase));
        var planner = new ModDependencyInstallPlanner(library, resolver, NullLogger<ModDependencyInstallPlanner>.Instance);
        return new ModpackService(
            new FixedPathProvider(temp.Paths),
            library,
            originResolver,
            resolver,
            planner,
            NullLogger<ModpackService>.Instance);
    }

    private static ModLibraryService CreateLibrary(TempAppPaths temp, Dictionary<int, byte[]> payloads)
    {
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            var fileId = int.Parse(url.Split("fileid=")[^1], CultureInfo.InvariantCulture);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payloads.GetValueOrDefault(fileId) ?? [])
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip") },
                },
            };
        });
        return new ModLibraryService(
            new FixedPathProvider(temp.Paths),
            NullLogger<ModLibraryService>.Instance,
            new HttpClient(handler));
    }

    private static void WriteManifestEntry(ZipArchive archive, ModpackManifest manifest)
    {
        var entry = archive.CreateEntry("manifest.json");
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(ModpackManifestCodec.Serialize(manifest));
    }

    private static byte[] CreateZip(string modId, string name, string version, string dependenciesJson)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("modinfo.json");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(
                $$"""{"modid":"{{modId}}","name":"{{name}}","version":"{{version}}","dependencies":{{dependenciesJson}}}""");
        }

        return stream.ToArray();
    }

    private sealed class StubReleaseResolver(Dictionary<string, ModReleaseInfo> releases) : IModReleaseResolver
    {
        public Task<Result<ModReleaseInfo>> ResolveAsync(
            string modIdentifier,
            string gameVersion,
            CancellationToken cancellationToken = default)
        {
            if (releases.TryGetValue(modIdentifier, out var release))
            {
                return Task.FromResult(Result<ModReleaseInfo>.Success(release));
            }

            return Task.FromResult(Result<ModReleaseInfo>.Failure("not found"));
        }
    }
}
