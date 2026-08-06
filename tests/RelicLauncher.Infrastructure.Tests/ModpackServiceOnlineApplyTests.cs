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

public class ModpackServiceOnlineApplyTests
{
    [Fact]
    public async Task ApplyAsync_OnlineMerge_InstallsResolvedRelease()
    {
        using var temp = new TempAppPaths();
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        Directory.CreateDirectory(Path.Combine(dataPath, "Mods"));

        var zipBytes = CreateZip("onlinemod", "Online", "1.0.0", "{}");
        var release = new ModReleaseInfo
        {
            FileId = 20,
            ModVersion = "1.0.0",
            FileName = "onlinemod.zip",
            CompatibleGameVersions = ["1.22.0"],
            DownloadUrl = "https://example.test/download?fileid=20",
        };
        var releases = new Dictionary<string, ModReleaseInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["20"] = release,
            ["onlinemod"] = release,
        };

        var library = CreateLibrary(temp, new Dictionary<int, byte[]> { [20] = zipBytes });
        var service = CreateService(temp, library, releases);

        var manifest = new ModpackManifest
        {
            Name = "Online Pack",
            GameVersion = "1.22.0",
            CreatedAt = DateTimeOffset.UtcNow,
            Distribution = ModpackDistribution.Online,
            Mods =
            [
                new ModpackModEntry
                {
                    ModId = "onlinemod",
                    ModVersion = "1.0.0",
                    FileId = 20,
                    Source = ModpackModSource.ModDb,
                },
            ],
        };

        var apply = await service.ApplyAsync(new ModpackApplyRequest
        {
            DataPath = dataPath,
            Manifest = manifest,
            Mode = ModpackApplyMode.Merge,
        });

        apply.IsSuccess.Should().BeTrue();
        apply.Value!.InstalledCount.Should().Be(1);
        var installed = await library.ListInstalledAsync(dataPath);
        installed.Value!.Should().ContainSingle(m => m.ModId == "onlinemod");
    }

    [Fact]
    public async Task ReadManifestAsync_RejectsUnsupportedFormat()
    {
        using var temp = new TempAppPaths();
        var packPath = Path.Combine(temp.Paths.RootDirectory, "bad.relicmodpack");
        using (var archive = ZipFile.Open(packPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("manifest.json");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write("""{"format":"other","name":"Bad","gameVersion":"1.22.0","mods":[]}""");
        }

        var library = CreateLibrary(temp, new Dictionary<int, byte[]>());
        var service = CreateService(temp, library, new Dictionary<string, ModReleaseInfo>(StringComparer.OrdinalIgnoreCase));

        var result = await service.ReadManifestAsync(packPath);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Could not read modpack");
    }

    [Fact]
    public async Task SaveLocalAsync_ThenListLocal_ReturnsSavedPack()
    {
        using var temp = new TempAppPaths();
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        var modsDir = Path.Combine(dataPath, "Mods");
        Directory.CreateDirectory(modsDir);
        var zipBytes = CreateZip("savedmod", "Saved", "1.0.0", "{}");
        await File.WriteAllBytesAsync(Path.Combine(modsDir, "mod_30.zip"), zipBytes);

        var indexPath = Path.Combine(temp.Paths.CacheDirectory, "mods", "files", "index.json");
        Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
        await File.WriteAllTextAsync(indexPath, """{"30":{"fileId":30,"fileName":"mod_30.zip","modId":"savedmod"}}""");

        var library = CreateLibrary(temp, new Dictionary<int, byte[]> { [30] = zipBytes });
        var service = CreateService(temp, library, new Dictionary<string, ModReleaseInfo>(StringComparer.OrdinalIgnoreCase));

        var mod = new LocalModInfo
        {
            Path = Path.Combine(modsDir, "mod_30.zip"),
            FileName = "mod_30.zip",
            ModId = "savedmod",
            Version = "1.0.0",
            IsEnabled = true,
        };

        var saved = await service.SaveLocalAsync(new ModpackSaveRequest
        {
            DataPath = dataPath,
            GameVersion = "1.22.0",
            Name = "Saved Pack",
            Description = "Local library test",
            Mods = [mod],
        });

        saved.IsSuccess.Should().BeTrue();
        var listed = await service.ListLocalAsync();
        listed.IsSuccess.Should().BeTrue();
        listed.Value!.Should().ContainSingle(p => p.Name == "Saved Pack" && p.ModCount == 1);
    }

    private static ModpackService CreateService(
        TempAppPaths temp,
        ModLibraryService library,
        Dictionary<string, ModReleaseInfo> releases)
    {
        var originResolver = new ModOriginResolver(new FixedPathProvider(temp.Paths));
        var resolver = new StubReleaseResolver(releases);
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
