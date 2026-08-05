using System.IO.Compression;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Infrastructure.DotNet;
using RelicLauncher.Infrastructure.Platform;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class DotNetRuntimeProvisionerTests
{
    [Fact]
    public async Task EnsureAsync_UsesManagedRoot_WhenAlreadyPresent()
    {
        using var temp = new TempAppPaths();
        var managed = Path.Combine(temp.Paths.CacheDirectory, "dotnet", "net8");
        SeedSharedFramework(managed, major: 8, requireDesktop: OperatingSystem.IsWindows());

        var provisioner = new DotNetRuntimeProvisioner(
            new FixedPathProvider(temp.Paths),
            new RuntimePlatform(),
            NullLogger<DotNetRuntimeProvisioner>.Instance,
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError))),
            () => Array.Empty<string>());

        var result = await provisioner.EnsureAsync(8);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsManagedByRelic.Should().BeTrue();
        result.Value.DotNetRoot.Should().Be(managed);
    }

    [Fact]
    public async Task EnsureAsync_UsesSystemRoot_WhenPresent()
    {
        using var temp = new TempAppPaths();
        var systemRoot = Path.Combine(temp.Paths.RootDirectory, "system-dotnet");
        SeedSharedFramework(systemRoot, major: 10, requireDesktop: OperatingSystem.IsWindows());

        var provisioner = new DotNetRuntimeProvisioner(
            new FixedPathProvider(temp.Paths),
            new RuntimePlatform(),
            NullLogger<DotNetRuntimeProvisioner>.Instance,
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError))),
            () => [systemRoot]);

        var result = await provisioner.EnsureAsync(10);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsManagedByRelic.Should().BeFalse();
        result.Value.DotNetRoot.Should().Be(systemRoot);
    }

    [Fact]
    public async Task EnsureAsync_DownloadsAndExtracts_WhenMissing()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var temp = new TempAppPaths();
        var archiveBytes = CreateTarGzWithSharedFramework(major: 8);
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.EndsWith("latest.version", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("8.0.29"),
                };
            }

            if (url.Contains("dotnet-runtime-8.0.29", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(archiveBytes),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var provisioner = new DotNetRuntimeProvisioner(
            new FixedPathProvider(temp.Paths),
            new RuntimePlatform(),
            NullLogger<DotNetRuntimeProvisioner>.Instance,
            new HttpClient(handler),
            () => Array.Empty<string>());

        var result = await provisioner.EnsureAsync(8);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsManagedByRelic.Should().BeTrue();
        Directory.Exists(Path.Combine(result.Value.DotNetRoot, "shared", "Microsoft.NETCore.App", "8.0.29"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task EnsureAsync_Fails_ForUnsupportedMajor()
    {
        using var temp = new TempAppPaths();
        var provisioner = new DotNetRuntimeProvisioner(
            new FixedPathProvider(temp.Paths),
            new RuntimePlatform(),
            NullLogger<DotNetRuntimeProvisioner>.Instance,
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            () => Array.Empty<string>());

        var result = await provisioner.EnsureAsync(9);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unsupported");
    }

    private static void SeedSharedFramework(string root, int major, bool requireDesktop)
    {
        var version = $"{major}.0.0";
        Directory.CreateDirectory(Path.Combine(root, "shared", "Microsoft.NETCore.App", version));
        File.WriteAllText(Path.Combine(root, "shared", "Microsoft.NETCore.App", version, "marker"), "ok");
        if (requireDesktop)
        {
            Directory.CreateDirectory(Path.Combine(root, "shared", "Microsoft.WindowsDesktop.App", version));
            File.WriteAllText(Path.Combine(root, "shared", "Microsoft.WindowsDesktop.App", version, "marker"), "ok");
        }
    }

    private static byte[] CreateTarGzWithSharedFramework(int major)
    {
        var version = $"{major}.0.29";
        using var tarBuffer = new MemoryStream();
        using (var tarWriter = new System.Formats.Tar.TarWriter(tarBuffer, leaveOpen: true))
        {
            var entryPath = $"shared/Microsoft.NETCore.App/{version}/marker";
            var entry = new System.Formats.Tar.V7TarEntry(System.Formats.Tar.TarEntryType.V7RegularFile, entryPath)
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes("ok")),
            };
            tarWriter.WriteEntry(entry);
        }

        tarBuffer.Position = 0;
        using var gzipBuffer = new MemoryStream();
        using (var gzip = new GZipStream(gzipBuffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            tarBuffer.CopyTo(gzip);
        }

        return gzipBuffer.ToArray();
    }
}
