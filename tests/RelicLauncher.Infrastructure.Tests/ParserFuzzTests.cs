using System.Text;
using System.Text.Json;
using FluentAssertions;
using RelicLauncher.Infrastructure.Mods;
using RelicLauncher.Infrastructure.News;
using RelicLauncher.Infrastructure.Versions;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ParserFuzzTests
{
    private static readonly string ValidCatalogJson = """
      {
        "1.22.6": {
          "windows": {
            "filename": "vs_install_win-x64_1.22.6.exe",
            "urls": { "cdn": "https://cdn.example/win.exe" }
          }
        }
      }
      """;

    private static readonly string ValidModsJson = """
      {
        "mods": [
          { "modid": 1, "name": "Test Mod", "downloads": 1 }
        ]
      }
      """;

    [Theory]
    [MemberData(nameof(MutatedCatalogInputs))]
    public void ParseCatalog_DoesNotThrow_OnMutatedJson(string input)
    {
        try
        {
            _ = VintageStoryVersionCatalog.ParseCatalog(input);
        }
        catch (JsonException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Theory]
    [MemberData(nameof(MutatedModsInputs))]
    public void ParseSearch_DoesNotThrow_OnMutatedJson(string input)
    {
        try
        {
            _ = ModDbClient.ParseSearch(input);
        }
        catch (JsonException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Theory]
    [MemberData(nameof(MutatedHtmlInputs))]
    public void ParseArticles_DoesNotThrow_OnMutatedHtml(string input)
    {
        _ = VintageStoryNewsService.ParseArticles(input, 5);
    }

    [Fact]
    public void ParseCatalog_Throws_OnUnparseableVersionEntries()
    {
        var json = """{ "1.0.0": { "windowsupdate": { "filename": "u.exe", "urls": { "cdn": "x" } } } }""";

        var act = () => VintageStoryVersionCatalog.ParseCatalog(json);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void ParseSearch_ReturnsEmpty_WhenModsPropertyMissing()
    {
        ModDbClient.ParseSearch("""{ "statuscode": 200 }""").Should().BeEmpty();
    }

    public static IEnumerable<object[]> MutatedCatalogInputs()
      => Mutate(ValidCatalogJson, 48).Select(static value => new object[] { value });

    public static IEnumerable<object[]> MutatedModsInputs()
      => Mutate(ValidModsJson, 48).Select(static value => new object[] { value });

    public static IEnumerable<object[]> MutatedHtmlInputs()
      => Mutate(VintageStoryNewsHtml.SingleArticle, 32).Select(static value => new object[] { value });

    private static IEnumerable<string> Mutate(string seed, int count)
    {
        var random = new Random(unchecked((int)0x5EED1234));
        var bytes = Encoding.UTF8.GetBytes(seed);
        for (var i = 0; i < count; i++)
        {
            var copy = (byte[])bytes.Clone();
            var mutations = random.Next(1, 6);
            for (var m = 0; m < mutations; m++)
            {
                var index = random.Next(copy.Length);
                copy[index] = (byte)random.Next(0, 256);
            }

            yield return Encoding.UTF8.GetString(copy);
        }
    }
}
