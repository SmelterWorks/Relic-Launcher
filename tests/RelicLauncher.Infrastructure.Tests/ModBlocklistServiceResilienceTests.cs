using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Endpoints;
using RelicLauncher.Infrastructure.Mods;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModBlocklistServiceResilienceTests
{
    private const string BlocklistJson = """
        [
          { "id": "waypointtogether@1.0.1", "reason": "Contains a unfixed vulnerability" }
        ]
        """;

    [Fact]
    public async Task GetEntriesAsync_FetchesAndCachesBlocklist()
    {
        using var temp = new TempAppPaths();
        var calls = 0;
        var service = CreateService(temp, _ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BlocklistJson),
            };
        });

        var first = await service.GetEntriesAsync();
        var second = await service.GetEntriesAsync();

        first.IsSuccess.Should().BeTrue();
        first.Value.Should().ContainSingle();
        second.IsSuccess.Should().BeTrue();
        calls.Should().Be(1);
        File.Exists(Path.Combine(temp.Paths.CacheDirectory, "mods", "blockedmods.json")).Should().BeTrue();
    }

    [Fact]
    public async Task GetEntriesAsync_ServesStaleDiskCache_WhenNetworkFails()
    {
        using var temp = new TempAppPaths();
        await WriteCacheAsync(temp, BlocklistJson, DateTimeOffset.UtcNow.AddHours(-12));
        var service = CreateService(temp, _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var result = await service.GetEntriesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task GetEntriesAsync_ReturnsFailure_WhenNetworkFailsAndNoCache()
    {
        using var temp = new TempAppPaths();
        var service = CreateService(temp, _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var result = await service.GetEntriesAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Could not load mod blocklist");
    }

    [Fact]
    public async Task FindMatchAsync_ReturnsNull_ForBlankModId()
    {
        using var temp = new TempAppPaths();
        var service = CreateService(temp, _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BlocklistJson),
        });

        var result = await service.FindMatchAsync("  ", "1.0.0");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void FindMatch_MatchesModId_WhenVersionMissing()
    {
        var entries = new[]
        {
            new ModBlocklistEntry { Id = "blocked@1.0.0", Reason = "bad" },
        };

        ModBlocklistService.FindMatch(entries, "blocked", null)!.Reason.Should().Be("bad");
    }

    [Fact]
    public void Parse_ReturnsEmpty_ForNonArrayJson()
    {
        ModBlocklistService.Parse("""{ "id": "x" }""").Should().BeEmpty();
    }

    private static ModBlocklistService CreateService(TempAppPaths temp, Func<HttpRequestMessage, HttpResponseMessage> handler)
        => new(
            new FixedPathProvider(temp.Paths),
            new EndpointProvider(),
            NullLogger<ModBlocklistService>.Instance,
            new HttpClient(new StubHandler(handler)));

    private static async Task WriteCacheAsync(TempAppPaths temp, string payloadJson, DateTimeOffset cachedAt)
    {
        var path = Path.Combine(temp.Paths.CacheDirectory, "mods", "blockedmods.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var wrapper = JsonSerializer.Serialize(new
        {
            cachedAt,
            entries = ModBlocklistService.Parse(payloadJson),
        });
        await File.WriteAllTextAsync(path, wrapper).ConfigureAwait(false);
    }
}
