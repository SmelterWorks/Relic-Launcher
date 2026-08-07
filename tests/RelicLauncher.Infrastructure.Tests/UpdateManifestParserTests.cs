using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Updates;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class UpdateManifestParserTests
{
    private const string StableManifest = """
        {
          "schemaVersion": 1,
          "product": "relic",
          "channel": "stable",
          "version": "1.2.3",
          "publishedAt": "2026-08-07T00:00:00Z",
          "releaseNotesUrl": "https://github.com/SmelterWorks/Relic-Launcher/releases/tag/v1.2.3",
          "assets": [
            {
              "installKind": "WindowsNsis",
              "rid": "win-x64",
              "filename": "relic-launcher-1.2.3-win-x64-setup.exe",
              "url": "https://smelterworks.com/files/relic/1.2.3/relic-launcher-1.2.3-win-x64-setup.exe",
              "sha256": "abc123",
              "sizeBytes": 123
            }
          ]
        }
        """;

    [Fact]
    public void Parse_ValidManifest_ReturnsUpdateInfo()
    {
        var result = UpdateManifestParser.Parse(StableManifest, LauncherUpdateChannel.Stable);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Version.Should().Be("1.2.3");
        result.Value.Assets.Should().HaveCount(1);
        result.Value.Assets[0].InstallKind.Should().Be("WindowsNsis");
    }

    [Fact]
    public void Parse_UnsupportedSchema_ReturnsFailure()
    {
        var json = StableManifest.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");
        var result = UpdateManifestParser.Parse(json, LauncherUpdateChannel.Stable);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void IsAllowedAssetUrl_RejectsNonSmelterWorksHost()
    {
        UpdateManifestParser.IsAllowedAssetUrl("https://github.com/download/file.exe").Should().BeFalse();
        UpdateManifestParser.IsAllowedAssetUrl("https://smelterworks.com/files/relic/1.0/file.exe").Should().BeTrue();
    }
}
