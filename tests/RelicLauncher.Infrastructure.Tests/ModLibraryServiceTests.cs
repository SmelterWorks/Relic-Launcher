using System.IO.Compression;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Mods;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModLibraryServiceTests
{
    [Fact]
    public async Task ListInstallUninstall_AndToggleDisabled_WorksOnDisk()
    {
        using var temp = new TempAppPaths();
        var data = Path.Combine(temp.Paths.RootDirectory, "data");
        var modsDir = Path.Combine(data, "Mods");
        Directory.CreateDirectory(modsDir);

        var zipPath = Path.Combine(modsDir, "sample.zip");
        await WriteModZipAsync(zipPath, "sample", "Sample Mod", "1.0.0");

        var service = CreateService(temp);
        var listed = await service.ListInstalledAsync(data);
        listed.IsSuccess.Should().BeTrue();
        listed.Value.Should().ContainSingle();
        listed.Value![0].Name.Should().Be("Sample Mod");
        listed.Value[0].IsEnabled.Should().BeTrue();

        var toggled = await service.SetEnabledAsync(listed.Value[0], enabled: false);
        toggled.IsSuccess.Should().BeTrue();
        toggled.Value!.IsEnabled.Should().BeFalse();
        toggled.Value.Path.Should().EndWith(".disabled");

        var removed = await service.UninstallAsync(toggled.Value);
        removed.IsSuccess.Should().BeTrue();
        (await service.ListInstalledAsync(data)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task InstallAsync_CachesByFileId_AndRemovesOlderSameModId()
    {
        using var temp = new TempAppPaths();
        var data = Path.Combine(temp.Paths.RootDirectory, "data");
        var modsDir = Path.Combine(data, "Mods");
        Directory.CreateDirectory(modsDir);

        var oldZip = Path.Combine(modsDir, "sample_1.0.0.zip");
        await WriteModZipAsync(oldZip, "sample", "Sample Mod", "1.0.0");

        var downloadCalls = 0;
        var payload = CreateModZipBytes("sample", "Sample Mod", "2.0.0");
        var handler = new CountingHandler(_ =>
        {
            downloadCalls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip") },
                },
            };
        });

        var service = CreateService(temp, new HttpClient(handler));
        var release = new ModReleaseInfo
        {
            FileId = 42,
            ModVersion = "2.0.0",
            FileName = "sample_2.0.0.zip",
            CompatibleGameVersions = ["1.22.6"],
            DownloadUrl = "https://example.test/download?fileid=42",
        };

        var first = await service.InstallAsync(data, release);
        first.IsSuccess.Should().BeTrue();
        first.Value!.FileName.Should().Be("sample_2.0.0.zip");
        first.Value.Version.Should().Be("2.0.0");
        downloadCalls.Should().Be(1);

        File.Exists(oldZip).Should().BeFalse();
        Directory.GetFiles(modsDir).Should().ContainSingle(path => path.EndsWith("sample_2.0.0.zip"));

        var cacheFile = Path.Combine(temp.Paths.CacheDirectory, "mods", "files", "42.zip");
        File.Exists(cacheFile).Should().BeTrue();

        File.Delete(Path.Combine(modsDir, "sample_2.0.0.zip"));
        var second = await service.InstallAsync(data, release);
        second.IsSuccess.Should().BeTrue();
        downloadCalls.Should().Be(1);
        File.Exists(Path.Combine(modsDir, "sample_2.0.0.zip")).Should().BeTrue();
    }

    [Fact]
    public async Task InstallAsync_Fails_WhenFileIdMissing()
    {
        using var temp = new TempAppPaths();
        var service = CreateService(temp, new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var result = await service.InstallAsync(
            Path.Combine(temp.Paths.RootDirectory, "data"),
            new ModReleaseInfo { FileId = 0, ModVersion = "1.0.0", DownloadUrl = "https://example.test/x" });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("file id");
    }

    [Fact]
    public async Task InstallAsync_Fails_WhenDownloadUrlMissing()
    {
        using var temp = new TempAppPaths();
        var service = CreateService(temp, new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var result = await service.InstallAsync(
            Path.Combine(temp.Paths.RootDirectory, "data"),
            new ModReleaseInfo { FileId = 1, ModVersion = "1.0.0", DownloadUrl = "   " });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("download URL");
    }

    [Fact]
    public async Task ListInstalledAsync_SkipsDotEntries()
    {
        using var temp = new TempAppPaths();
        var data = Path.Combine(temp.Paths.RootDirectory, "data");
        var modsDir = Path.Combine(data, "Mods");
        Directory.CreateDirectory(modsDir);
        await File.WriteAllTextAsync(Path.Combine(modsDir, ".hidden"), "x");
        await WriteModZipAsync(Path.Combine(modsDir, "visible.zip"), "visible", "Visible", "1.0.0");

        var service = CreateService(temp);
        var listed = await service.ListInstalledAsync(data);

        listed.Value.Should().ContainSingle();
        listed.Value![0].ModId.Should().Be("visible");
    }

    [Fact]
    public async Task CleanDuplicateModsAsync_KeepsNewestPerModId()
    {
        using var temp = new TempAppPaths();
        var data = Path.Combine(temp.Paths.RootDirectory, "data");
        var modsDir = Path.Combine(data, "Mods");
        Directory.CreateDirectory(modsDir);

        await WriteModZipAsync(Path.Combine(modsDir, "sample_1.0.0.zip"), "sample", "Sample", "1.0.0");
        await WriteModZipAsync(Path.Combine(modsDir, "sample_2.0.0.zip"), "sample", "Sample", "2.0.0");
        await WriteModZipAsync(Path.Combine(modsDir, "other_1.0.0.zip"), "other", "Other", "1.0.0");

        var service = CreateService(temp);
        var cleaned = await service.CleanDuplicateModsAsync(data);
        cleaned.IsSuccess.Should().BeTrue();
        cleaned.Value.Should().Be(1);

        var listed = await service.ListInstalledAsync(data);
        listed.Value.Should().HaveCount(2);
        listed.Value!.Should().Contain(m => m.ModId == "sample" && m.Version == "2.0.0");
        listed.Value.Should().Contain(m => m.ModId == "other");
        listed.Value.Should().NotContain(m => m.Version == "1.0.0" && m.ModId == "sample");
    }

    private static ModLibraryService CreateService(TempAppPaths temp, HttpClient? client = null)
        => client is null
            ? new ModLibraryService(new FixedPathProvider(temp.Paths), NullLogger<ModLibraryService>.Instance)
            : new ModLibraryService(new FixedPathProvider(temp.Paths), NullLogger<ModLibraryService>.Instance, client);

    private static Task WriteModZipAsync(string path, string modId, string name, string version)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("modinfo.json");
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write($$"""{"modid":"{{modId}}","name":"{{name}}","version":"{{version}}"}""");
        return Task.CompletedTask;
    }

    private static byte[] CreateModZipBytes(string modId, string name, string version)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("modinfo.json");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($$"""{"modid":"{{modId}}","name":"{{name}}","version":"{{version}}"}""");
        }

        return stream.ToArray();
    }

    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
