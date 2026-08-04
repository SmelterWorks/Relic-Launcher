using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Endpoints;
using RelicLauncher.Infrastructure.Mods;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModDbClientResilienceTests
{
    private const string SampleCatalogJson = """
      {
        "statuscode": 200,
        "mods": [
          {
            "modid": 6,
            "name": "Carry Capacity",
            "downloads": 123,
            "urlalias": "carrycapacity"
          }
        ]
      }
      """;

    [Fact]
    public async Task SearchAsync_ServesExpiredDiskCache_WhenNetworkFails()
    {
        using var temp = new TempAppPaths();
        await WriteExpiredCatalogCacheAsync(temp);

        var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
        var client = CreateClient(temp, handler);

        var result = await client.SearchAsync(new ModSearchQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Mods.Should().ContainSingle();
        result.Value.Mods[0].Name.Should().Be("Carry Capacity");
        result.Value.FromCache.Should().BeTrue();
        result.Value.IsStale.Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_ReturnsFailure_WhenNetworkFailsAndNoCache()
    {
        using var temp = new TempAppPaths();
        var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
        var client = CreateClient(temp, handler);

        var result = await client.SearchAsync(new ModSearchQuery());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetModAsync_ServesStaleDetails_WhenNetworkFails()
    {
        using var temp = new TempAppPaths();
        await WriteDetailsCacheAsync(temp, "carrycapacity", expired: true);

        var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
        var client = CreateClient(temp, handler);

        var result = await client.GetModAsync("carrycapacity");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Carry Capacity");
    }

    [Fact]
    public void ParseSearch_Throws_WhenModsFieldIsNotArray()
    {
        var json = """{ "mods": "broken" }""";

        var act = () => ModDbClient.ParseSearch(json);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public async Task SearchAsync_ReturnsFailure_WhenCatalogResponseMissingModsProperty()
    {
        using var temp = new TempAppPaths();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "statuscode": 200 }""", Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(temp, handler);

        var result = await client.SearchAsync(new ModSearchQuery { PreferCache = false });

        result.IsSuccess.Should().BeFalse();
    }

    private static ModDbClient CreateClient(TempAppPaths temp, HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://mods.vintagestory.at/api/"),
        };
        return new ModDbClient(
          new FixedPathProvider(temp.Paths),
          new EndpointProvider(),
          NullLogger<ModDbClient>.Instance,
          httpClient);
    }

    private static async Task WriteExpiredCatalogCacheAsync(TempAppPaths temp)
    {
        var path = Path.Combine(temp.Paths.CacheDirectory, "mods", "catalog.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var payload = new
        {
            cachedAt = DateTimeOffset.UtcNow.AddHours(-12),
            mods = new List<ModSummary>
      {
        new()
        {
          ModId = 6,
          Name = "Carry Capacity",
          Downloads = 123,
          UrlAlias = "carrycapacity",
        },
      },
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload)).ConfigureAwait(false);
    }

    private static async Task WriteDetailsCacheAsync(TempAppPaths temp, string key, bool expired)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant())))[..20]
          .ToLowerInvariant();
        var path = Path.Combine(temp.Paths.CacheDirectory, "mods", "details", hash + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var payload = new
        {
            cachedAt = DateTimeOffset.UtcNow.AddHours(expired ? -12 : 0),
            mod = new ModDetails
            {
                ModId = 6,
                Name = "Carry Capacity",
                Downloads = 123,
            },
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload)).ConfigureAwait(false);
    }
}
