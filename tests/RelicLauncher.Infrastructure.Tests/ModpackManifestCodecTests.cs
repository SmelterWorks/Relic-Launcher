using System.Text.Json;
using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Modpacks;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModpackManifestCodecTests
{
    [Fact]
    public void RoundTrip_PreservesOnlineModEntry()
    {
        var manifest = new ModpackManifest
        {
            Name = "Test Pack",
            Description = "Notes",
            GameVersion = "1.22.0",
            CreatedAt = DateTimeOffset.Parse("2026-08-06T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            RelicVersion = "0.1.0",
            Distribution = ModpackDistribution.Online,
            Mods =
            [
                new ModpackModEntry
                {
                    ModId = "carrycapacity",
                    ModVersion = "1.2.0",
                    FileId = 12345,
                    Enabled = true,
                    Source = ModpackModSource.ModDb,
                },
            ],
        };

        var json = ModpackManifestCodec.Serialize(manifest);
        json.Should().Contain("\"distribution\": \"online\"");
        json.Should().Contain("\"source\": \"moddb\"");

        var roundTrip = ModpackManifestCodec.Deserialize(json);
        roundTrip.Name.Should().Be("Test Pack");
        roundTrip.Distribution.Should().Be(ModpackDistribution.Online);
        roundTrip.Mods.Should().ContainSingle(m =>
            m.ModId == "carrycapacity"
            && m.FileId == 12345
            && m.Source == ModpackModSource.ModDb);
    }

    [Fact]
    public void Deserialize_OfflineLocalEntry_ParsesArchivePath()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "format": "relic-modpack",
              "name": "Offline",
              "gameVersion": "1.22.0",
              "createdAt": "2026-08-06T12:00:00Z",
              "relicVersion": "0.1.0",
              "distribution": "offline",
              "mods": [
                {
                  "modId": "mydevmod",
                  "modVersion": "0.1.0",
                  "fileId": 0,
                  "enabled": true,
                  "source": "local",
                  "archivePath": "mods/mydevmod.zip"
                }
              ]
            }
            """;

        var manifest = ModpackManifestCodec.Deserialize(json);
        manifest.Distribution.Should().Be(ModpackDistribution.Offline);
        manifest.Mods.Should().ContainSingle(m =>
            m.Source == ModpackModSource.Local
            && m.ArchivePath == "mods/mydevmod.zip");
    }

    [Fact]
    public void Deserialize_EmptyJson_ThrowsJsonException()
    {
        var act = () => ModpackManifestCodec.Deserialize("null");
        act.Should().Throw<JsonException>();
    }
}
