using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Infrastructure.Mods;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModLibraryImportTests
{
    [Fact]
    public async Task ImportLocalAsync_CopiesFolderWithModInfo()
    {
        using var temp = new TempAppPaths();
        var data = Path.Combine(temp.Paths.RootDirectory, "data");
        var source = Path.Combine(temp.Paths.RootDirectory, "source", "wildlifeethology");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(
            Path.Combine(source, "modinfo.json"),
            """{"modid":"wildlifeethology","name":"Wildlife Ethology","version":"0.1.2"}""");
        Directory.CreateDirectory(Path.Combine(source, "assets"));
        await File.WriteAllTextAsync(Path.Combine(source, "assets", "note.txt"), "ok");

        var service = new ModLibraryService(
            new FixedPathProvider(temp.Paths),
            NullLogger<ModLibraryService>.Instance);

        var result = await service.ImportLocalAsync(data, source);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ModId.Should().Be("wildlifeethology");
        result.Value.IsDirectory.Should().BeTrue();
        File.Exists(Path.Combine(data, "Mods", "wildlifeethology", "modinfo.json")).Should().BeTrue();
        File.Exists(Path.Combine(data, "Mods", "wildlifeethology", "assets", "note.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task ImportLocalAsync_CopiesZip()
    {
        using var temp = new TempAppPaths();
        var data = Path.Combine(temp.Paths.RootDirectory, "data");
        var zipPath = Path.Combine(temp.Paths.RootDirectory, "sample.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("modinfo.json");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write("""{"modid":"sample","name":"Sample","version":"1.0.0"}""");
        }

        var service = new ModLibraryService(
            new FixedPathProvider(temp.Paths),
            NullLogger<ModLibraryService>.Instance);

        var result = await service.ImportLocalAsync(data, zipPath);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FileName.Should().Be("sample.zip");
        File.Exists(Path.Combine(data, "Mods", "sample.zip")).Should().BeTrue();
    }
}
