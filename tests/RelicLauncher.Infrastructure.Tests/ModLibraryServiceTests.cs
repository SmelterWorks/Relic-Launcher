using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
        await using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("modinfo.json");
            await using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            await writer.WriteAsync("""{"modid":"sample","name":"Sample Mod","version":"1.0.0"}""");
        }

        var service = new ModLibraryService(NullLogger<ModLibraryService>.Instance);
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
}
