using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Infrastructure.Caching;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class DiskRemoteImageCacheTests
{
    [Fact]
    public async Task GetImageBytesAsync_ReturnsNull_ForBlankUrl()
    {
        using var temp = new TempAppPaths();
        using var cache = CreateCache(temp, _ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await cache.GetImageBytesAsync("   ");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("//cdn.example/logo.png", "https://cdn.example/logo.png")]
    [InlineData("https://cdn.example/banner.jpg", "https://cdn.example/banner.jpg")]
    public void NormalizeUrl_NormalizesProtocolRelativeUrls(string input, string expected)
        => DiskRemoteImageCache.NormalizeUrl(input).Should().Be(expected);

    [Fact]
    public async Task GetImageBytesAsync_FetchesAndCachesOnDisk()
    {
        using var temp = new TempAppPaths();
        var payload = Encoding.UTF8.GetBytes("image-bytes");
        var calls = 0;
        using var cache = CreateCache(temp, request =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png") },
                },
            };
        });

        var url = "https://example.test/assets/modicon.png";
        var first = await cache.GetImageBytesAsync(url);
        var second = await cache.GetImageBytesAsync(url);

        first.Should().Equal(payload);
        second.Should().Equal(payload);
        calls.Should().Be(1);
        Directory.GetFiles(Path.Combine(temp.Paths.CacheDirectory, "images"), "*.png").Should().ContainSingle();
    }

    [Fact]
    public async Task GetImageBytesAsync_ReadsFromDisk_WhenMemoryEvicted()
    {
        using var temp = new TempAppPaths();
        var payload = Encoding.UTF8.GetBytes("cached-on-disk");
        var calls = 0;
        using var cache = CreateCache(temp, _ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            };
        });

        var url = "https://example.test/assets/icon.webp";
        (await cache.GetImageBytesAsync(url)).Should().Equal(payload);

        using var another = CreateCache(temp, _ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        (await another.GetImageBytesAsync(url)).Should().Equal(payload);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GetImageBytesAsync_ReturnsNull_WhenResponseTooLarge()
    {
        using var temp = new TempAppPaths();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[8]),
        };
        response.Content.Headers.ContentLength = RelicDefaults.MaxRemoteImageBytes + 1;
        using var cache = CreateCache(temp, _ => response);

        var result = await cache.GetImageBytesAsync("https://example.test/huge.png");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetImageBytesAsync_ReturnsNull_WhenHttpFails()
    {
        using var temp = new TempAppPaths();
        using var cache = CreateCache(temp, _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await cache.GetImageBytesAsync("https://example.test/missing.png");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("https://example.test/a.png", ".png")]
    [InlineData("https://example.test/a.jpg", ".jpg")]
    [InlineData("https://example.test/a.jpeg", ".jpg")]
    [InlineData("https://example.test/a.webp", ".webp")]
    [InlineData("https://example.test/a.bin", ".img")]
    public async Task GetImageBytesAsync_UsesExpectedFileExtension(string url, string extension)
    {
        using var temp = new TempAppPaths();
        using var cache = CreateCache(temp, _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("bytes")),
        });

        await cache.GetImageBytesAsync(url);

        Directory.GetFiles(Path.Combine(temp.Paths.CacheDirectory, "images"), $"*{extension}")
            .Should().ContainSingle();
    }

    private static DiskRemoteImageCache CreateCache(TempAppPaths temp, Func<HttpRequestMessage, HttpResponseMessage> handler)
        => new(
            new FixedPathProvider(temp.Paths),
            NullLogger<DiskRemoteImageCache>.Instance,
            new HttpClient(new StubHandler(handler)));
}
