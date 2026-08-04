using FluentAssertions;
using RelicLauncher.Infrastructure.Mods;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModDbClientTests
{
    [Fact]
    public void ParseSearch_ReadsModsArray()
    {
        var json = """
            {
              "statuscode": 200,
              "mods": [
                {
                  "modid": 6,
                  "assetid": 100,
                  "name": "Carry Capacity",
                  "author": "Someone",
                  "summary": "Carry more",
                  "downloads": 123,
                  "urlalias": "carrycapacity"
                }
              ]
            }
            """;

        var mods = ModDbClient.ParseSearch(json);

        mods.Should().ContainSingle();
        mods[0].ModId.Should().Be(6);
        mods[0].Name.Should().Be("Carry Capacity");
        mods[0].UrlAlias.Should().Be("carrycapacity");
        mods[0].LogoUrl.Should().BeNull();
    }

    [Fact]
    public void ParseSearch_ReadsLogoAndTags()
    {
        var json = """
            {
              "statuscode": 200,
              "mods": [
                {
                  "modid": 6,
                  "name": "Carry Capacity",
                  "logo": "https://cdn.example/logo.png",
                  "tags": ["QoL", "Storage"],
                  "follows": 10,
                  "downloads": 123
                }
              ]
            }
            """;

        var mods = ModDbClient.ParseSearch(json);

        mods[0].LogoUrl.Should().Be("https://cdn.example/logo.png");
        mods[0].Tags.Should().Equal("QoL", "Storage");
        mods[0].Follows.Should().Be(10);
    }

    [Fact]
    public void ParseDetails_ReadsScreenshotsAndLinks()
    {
        var json = """
            {
              "statuscode": 200,
              "mod": {
                "modid": 6,
                "urlalias": "carrycapacity",
                "name": "Carry Capacity",
                "logofile": "https://cdn.example/logo.png",
                "text": "<p>Hello</p><p>world</p>",
                "homepageurl": "https://example.com",
                "screenshots": [
                  {
                    "fileid": 1,
                    "mainfile": "https://cdn.example/a.jpg",
                    "thumbnailfilename": "https://cdn.example/a-thumb.jpg"
                  }
                ],
                "releases": [
                  {
                    "fileid": 42,
                    "modversion": "1.2.3",
                    "filename": "carry.zip",
                    "tags": ["1.22.6"]
                  }
                ]
              }
            }
            """;

        var details = ModDbClient.ParseDetails(json);

        details.Should().NotBeNull();
        details!.UrlAlias.Should().Be("carrycapacity");
        details.LogoUrl.Should().Be("https://cdn.example/logo.png");
        details.DescriptionText.Should().Contain("Hello");
        details.DescriptionText.Should().Contain("world");
        details.DescriptionText.Should().NotContain("<");
        details.DescriptionText.Should().Contain("\n");
        details.HomepageUrl.Should().Be("https://example.com");
        details.Screenshots.Should().ContainSingle();
        details.Releases.Should().ContainSingle();
        details.Releases[0].FileId.Should().Be(42);
        details.Releases[0].DownloadUrl.Should().Contain("fileid=42");
    }

    [Fact]
    public void ParseTags_ReadsTagCatalog()
    {
        var json = """
            {
              "statuscode": "200",
              "tags": [
                { "tagid": "467", "name": "Absolute Cinema", "color": "#92C96AFF" },
                { "tagid": 285, "name": "Accessibility", "color": "#92C96AFF" }
              ]
            }
            """;

        var tags = ModDbClient.ParseTags(json);

        tags.Should().HaveCount(2);
        tags.Select(t => t.Name).Should().Contain(["Absolute Cinema", "Accessibility"]);
        tags.Should().Contain(t => t.TagId == "467");
        tags.Should().Contain(t => t.TagId == "285");
    }
}
