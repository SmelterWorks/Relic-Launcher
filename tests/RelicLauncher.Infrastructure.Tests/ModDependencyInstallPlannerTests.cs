using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Mods;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModDependencyInstallPlannerTests
{
    [Fact]
    public async Task PlanAsync_OrdersDependenciesBeforeRoot()
    {
        using var temp = new TempAppPaths();
        var payloads = new Dictionary<int, byte[]>
        {
            [1] = CreateZip("root", "Root", "1.0.0", """{"game":"*","lib":"1.0.0"}"""),
            [2] = CreateZip("lib", "Lib", "1.2.0", """{"game":"*"}"""),
        };

        var library = CreateLibrary(temp, payloads);
        var resolver = new StubReleaseResolver(new Dictionary<string, ModReleaseInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["lib"] = Release(2, "1.2.0", "lib.zip"),
        });
        var planner = new ModDependencyInstallPlanner(
            library,
            resolver,
            NullLogger<ModDependencyInstallPlanner>.Instance);

        var plan = await planner.PlanAsync(Release(1, "1.0.0", "root.zip"), "1.22.0", []);

        plan.IsSuccess.Should().BeTrue();
        var installs = plan.Value!.ReleasesToInstall.ToList();
        installs.Should().HaveCount(2);
        installs[0].ModId.Should().Be("lib");
        installs[0].Depth.Should().Be(1);
        installs[^1].ModId.Should().Be("root");
        installs[^1].Depth.Should().Be(0);
        plan.Value.Unresolved.Should().BeEmpty();
    }

    [Fact]
    public async Task PlanAsync_SkipsAlreadyInstalledDependency()
    {
        using var temp = new TempAppPaths();
        var payloads = new Dictionary<int, byte[]>
        {
            [1] = CreateZip("root", "Root", "1.0.0", """{"lib":"1.0.0"}"""),
        };

        var library = CreateLibrary(temp, payloads);
        var resolver = new StubReleaseResolver(new Dictionary<string, ModReleaseInfo>(StringComparer.OrdinalIgnoreCase));
        var planner = new ModDependencyInstallPlanner(
            library,
            resolver,
            NullLogger<ModDependencyInstallPlanner>.Instance);

        var installed = new[]
        {
            new LocalModInfo
            {
                Path = "/mods/lib.zip",
                FileName = "lib.zip",
                ModId = "lib",
                Name = "Lib",
                Version = "1.5.0",
                IsEnabled = true,
                Dependencies = [],
            },
        };

        var plan = await planner.PlanAsync(Release(1, "1.0.0", "root.zip"), "1.22.0", installed);

        plan.IsSuccess.Should().BeTrue();
        plan.Value!.ReleasesToInstall.Should().ContainSingle(s => s.ModId == "root");
        resolver.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task PlanAsync_MarksUnresolvedWhenModDbFails()
    {
        using var temp = new TempAppPaths();
        var payloads = new Dictionary<int, byte[]>
        {
            [1] = CreateZip("root", "Root", "1.0.0", """{"missinglib":"1.0.0"}"""),
        };

        var library = CreateLibrary(temp, payloads);
        var resolver = new StubReleaseResolver(new Dictionary<string, ModReleaseInfo>(StringComparer.OrdinalIgnoreCase));
        var planner = new ModDependencyInstallPlanner(
            library,
            resolver,
            NullLogger<ModDependencyInstallPlanner>.Instance);

        var plan = await planner.PlanAsync(Release(1, "1.0.0", "root.zip"), "1.22.0", []);

        plan.IsSuccess.Should().BeTrue();
        plan.Value!.Unresolved.Should().ContainSingle(u => u.ModId == "missinglib" && u.IsUnresolved);
        plan.Value.ReleasesToInstall.Should().ContainSingle(s => s.ModId == "root");
    }

    private static ModLibraryService CreateLibrary(TempAppPaths temp, Dictionary<int, byte[]> payloads)
    {
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            var fileId = int.Parse(url.Split("fileid=")[^1], CultureInfo.InvariantCulture);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payloads[fileId])
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

    private static ModReleaseInfo Release(int fileId, string version, string fileName)
        => new()
        {
            FileId = fileId,
            ModVersion = version,
            FileName = fileName,
            CompatibleGameVersions = ["1.22.0"],
            DownloadUrl = $"https://example.test/download?fileid={fileId}",
        };

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
        public List<string> Calls { get; } = [];

        public Task<Result<ModReleaseInfo>> ResolveAsync(
            string modIdentifier,
            string gameVersion,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(modIdentifier);
            if (releases.TryGetValue(modIdentifier, out var release))
            {
                return Task.FromResult(Result<ModReleaseInfo>.Success(release));
            }

            return Task.FromResult(Result<ModReleaseInfo>.Failure("not found"));
        }
    }
}
