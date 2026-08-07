using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Mods;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModLibraryIconTests
{
    [Fact]
    public void TryReadModIcon_ReadsModiconFromFolder()
    {
        using var temp = new TempAppPaths();
        var modDir = Path.Combine(temp.Paths.RootDirectory, "iconmod");
        Directory.CreateDirectory(modDir);
        File.WriteAllText(Path.Combine(modDir, "modinfo.json"), """{"modid":"iconmod","name":"Icon Mod","version":"1.0.0"}""");
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        File.WriteAllBytes(Path.Combine(modDir, "modicon.png"), png);

        var service = new ModLibraryService(new FixedPathProvider(temp.Paths), NullLogger<ModLibraryService>.Instance);
        var info = new LocalModInfo
        {
            Path = modDir,
            FileName = "iconmod",
            ModId = "iconmod",
            Name = "Icon Mod",
            IsDirectory = true,
        };

        var bytes = service.TryReadModIcon(info);
        bytes.Should().NotBeNull();
        bytes!.Length.Should().Be(png.Length);
    }

    [Fact]
    public void TryReadModIcon_ReadsIconPathFromZip()
    {
        using var temp = new TempAppPaths();
        var zipPath = Path.Combine(temp.Paths.RootDirectory, "zipicon.zip");
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("modinfo.json").Open(), Encoding.UTF8))
            {
                writer.Write("""{"modid":"zipicon","name":"Zip Icon","version":"1.0.0","iconPath":"assets/icon.png"}""");
            }

            using var icon = zip.CreateEntry("assets/icon.png").Open();
            icon.Write(png, 0, png.Length);
        }

        var service = new ModLibraryService(new FixedPathProvider(temp.Paths), NullLogger<ModLibraryService>.Instance);
        var info = new LocalModInfo
        {
            Path = zipPath,
            FileName = "zipicon.zip",
            ModId = "zipicon",
            Name = "Zip Icon",
            IconPath = "assets/icon.png",
            IsDirectory = false,
        };

        var bytes = service.TryReadModIcon(info);
        bytes.Should().NotBeNull();
        bytes!.Length.Should().Be(png.Length);
    }

    [Fact]
    public void TryReadModIcon_RejectsTraversalIconPath()
    {
        using var temp = new TempAppPaths();
        var modDir = Path.Combine(temp.Paths.RootDirectory, "escapemod");
        var secretDir = Path.Combine(temp.Paths.RootDirectory, "secret");
        Directory.CreateDirectory(modDir);
        Directory.CreateDirectory(secretDir);
        File.WriteAllText(Path.Combine(modDir, "modinfo.json"), """{"modid":"escapemod","name":"Escape","version":"1.0.0","iconPath":"../secret/leak.png"}""");
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        File.WriteAllBytes(Path.Combine(secretDir, "leak.png"), png);

        var service = new ModLibraryService(new FixedPathProvider(temp.Paths), NullLogger<ModLibraryService>.Instance);
        var info = new LocalModInfo
        {
            Path = modDir,
            FileName = "escapemod",
            ModId = "escapemod",
            Name = "Escape",
            IconPath = "../secret/leak.png",
            IsDirectory = true,
        };

        service.TryReadModIcon(info).Should().BeNull();
    }
}
