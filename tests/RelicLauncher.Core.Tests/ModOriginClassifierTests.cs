using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class ModOriginClassifierTests
{
    [Fact]
    public void Classify_ModDbZipName_ReturnsModDbFileId()
    {
        var mod = new LocalModInfo
        {
            Path = "/mods/mod_42.zip",
            FileName = "mod_42.zip",
            ModId = "carrycapacity",
            IsEnabled = true,
        };

        var origin = ModOriginClassifier.Classify(mod, []);
        origin.Source.Should().Be(ModpackModSource.ModDb);
        origin.FileId.Should().Be(42);
    }

    [Fact]
    public void Classify_Directory_ReturnsLocal()
    {
        var mod = new LocalModInfo
        {
            Path = "/mods/mydevmod",
            FileName = "mydevmod",
            ModId = "mydevmod",
            IsDirectory = true,
            IsEnabled = true,
        };

        var origin = ModOriginClassifier.Classify(mod, []);
        origin.Source.Should().Be(ModpackModSource.Local);
        origin.FileId.Should().Be(0);
    }

    [Fact]
    public void Classify_IndexMatch_ReturnsModDbFileId()
    {
        var mod = new LocalModInfo
        {
            Path = "/mods/custom-name.zip",
            FileName = "custom-name.zip",
            ModId = "carrycapacity",
            IsEnabled = true,
        };
        var index = new List<ModFileIndexEntry>
        {
            new() { FileId = 99, FileName = "custom-name.zip", ModId = "carrycapacity" },
        };

        var origin = ModOriginClassifier.Classify(mod, index);
        origin.Source.Should().Be(ModpackModSource.ModDb);
        origin.FileId.Should().Be(99);
    }

    [Fact]
    public void RequiresOfflinePack_WhenAnyLocal_ReturnsTrue()
    {
        var origins = new List<ModOriginInfo>
        {
            new() { Source = ModpackModSource.ModDb, FileId = 1 },
            new() { Source = ModpackModSource.Local, FileId = 0 },
        };

        ModOriginClassifier.RequiresOfflinePack(origins).Should().BeTrue();
    }
}
