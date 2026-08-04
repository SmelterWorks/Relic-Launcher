using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Infrastructure.Endpoints;
using RelicLauncher.Infrastructure.Versions;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class VintageStoryVersionCatalogResilienceTests
{
  private const string SampleCatalogJson = """
      {
        "1.22.6": {
          "windows": {
            "filename": "vs_install_win-x64_1.22.6.exe",
            "urls": { "cdn": "https://cdn.example/win.exe" }
          }
        }
      }
      """;

  [Fact]
  public async Task GetVersionsAsync_ServesExpiredDiskCache_WhenNetworkFails()
  {
    using var temp = new TempAppPaths();
    await WriteExpiredCatalogCacheAsync(temp, SampleCatalogJson).ConfigureAwait(false);

    var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
    var catalog = CreateCatalog(temp, handler);

    var result = await catalog.GetVersionsAsync();

    result.IsSuccess.Should().BeTrue();
    result.Value.Should().ContainSingle();
    result.Value![0].Version.Should().Be("1.22.6");
    catalog.LastCatalogWasStale.Should().BeTrue();
  }

  [Fact]
  public async Task GetVersionsAsync_ReturnsFailure_WhenNetworkFailsAndNoCache()
  {
    using var temp = new TempAppPaths();
    var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
    var catalog = CreateCatalog(temp, handler);

    var result = await catalog.GetVersionsAsync();

    result.IsSuccess.Should().BeFalse();
    result.Error.Should().Contain("Could not load version catalog");
  }

  [Fact]
  public void ParseCatalog_Throws_WhenEntriesExistButNoneParse()
  {
    var json = """
        {
          "1.22.6": {
            "linuxserver": {
              "filename": "server.tar.gz",
              "urls": { "cdn": "https://cdn.example/server.tar.gz" }
            }
          }
        }
        """;

    var act = () => VintageStoryVersionCatalog.ParseCatalog(json);

    act.Should().Throw<JsonException>();
  }

  [Fact]
  public async Task GetLatestStableVersionAsync_ServesStaleDiskCache_WhenNetworkFails()
  {
    using var temp = new TempAppPaths();
    var latestPath = Path.Combine(temp.Paths.CacheDirectory, "versions", "lateststable.txt");
    Directory.CreateDirectory(Path.GetDirectoryName(latestPath)!);
    await File.WriteAllTextAsync(latestPath, "1.22.6").ConfigureAwait(false);
    File.SetLastWriteTimeUtc(latestPath, DateTime.UtcNow.AddHours(-12));

    var handler = new StubHandler(request =>
    {
      if (request.RequestUri!.AbsoluteUri.Contains("lateststable", StringComparison.OrdinalIgnoreCase))
      {
        throw new HttpRequestException("offline");
      }

      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(SampleCatalogJson, Encoding.UTF8, "application/json"),
      };
    });
    var catalog = CreateCatalog(temp, handler);

    var result = await catalog.GetLatestStableVersionAsync();

    result.IsSuccess.Should().BeTrue();
    result.Value.Should().Be("1.22.6");
  }

  private static VintageStoryVersionCatalog CreateCatalog(TempAppPaths temp, HttpMessageHandler handler)
  {
    var httpClient = new HttpClient(handler);
    return new VintageStoryVersionCatalog(
      new FixedPathProvider(temp.Paths),
      new EndpointProvider(),
      NullLogger<VintageStoryVersionCatalog>.Instance,
      httpClient);
  }

  private static async Task WriteExpiredCatalogCacheAsync(TempAppPaths temp, string payloadJson)
  {
    var path = Path.Combine(temp.Paths.CacheDirectory, "versions", "catalog.json");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var wrapper = JsonSerializer.Serialize(new
    {
      cachedAt = DateTimeOffset.UtcNow.AddHours(-12),
      payload = payloadJson,
    });
    await File.WriteAllTextAsync(path, wrapper).ConfigureAwait(false);
  }
}
