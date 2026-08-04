using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Mods;
using RelicLauncher.Infrastructure.Versions;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class BoundedDownloadTests
{
    [Fact]
    public async Task ModInstall_RejectsOversizedContentLength()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[16]),
            };
            response.Content.Headers.ContentLength = 1024L * 1024L * 1024L;
            return response;
        });

        using var temp = new TempAppPaths();
        var service = new ModLibraryService(
            new FixedPathProvider(temp.Paths),
            NullLogger<ModLibraryService>.Instance,
            new HttpClient(handler));
        var dataRoot = Path.Combine(temp.Paths.RootDirectory, "data");
        Directory.CreateDirectory(dataRoot);
        var result = await service.InstallAsync(dataRoot, new ModReleaseInfo
        {
            FileId = 1,
            ModVersion = "1.0.0",
            FileName = "big.zip",
            DownloadUrl = "https://example.test/big.zip",
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("maximum");
    }

    [Fact]
    public async Task GameDownload_RejectsOversizedContentLength()
    {
        using var temp = new TempAppPaths();
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[8]),
            };
            response.Content.Headers.ContentLength = 8L * 1024L * 1024L * 1024L;
            return response;
        });

        var installer = new GameVersionInstaller(
            new FixedPathProvider(temp.Paths),
            new FakeInstalledStore(),
            new FakePlatform(),
            NullLogger<GameVersionInstaller>.Instance,
            new HttpClient(handler));

        var package = new GameVersionPackage
        {
            PlatformKey = "linux",
            Kind = ClientPackageKind.TarGz,
            CdnUrl = "https://example.test/game.tgz",
            FileName = "game.tgz",
        };

        var archivePath = Path.Combine(temp.Paths.CacheDirectory, "game.tgz");
        Directory.CreateDirectory(temp.Paths.CacheDirectory);
        var result = await installer.DownloadAsync(package, archivePath, progress: null, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("maximum");
    }

    private sealed class FakeInstalledStore : IInstalledVersionStore
    {
        public Task<Result<IReadOnlyList<InstalledGameVersion>>> ListAsync(string installsRoot, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<InstalledGameVersion>>.Success([]));

        public Task<Result> SaveAsync(string installsRoot, IReadOnlyList<InstalledGameVersion> versions, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class FakePlatform : IRuntimePlatform
    {
        public PlatformInfo GetPlatformInfo() => new()
        {
            Os = HostOs.Linux,
            Arch = HostArch.X64,
            ClientPackageKey = "linux",
            DefaultDataPath = "/tmp/data",
            DefaultInstallsRoot = "/tmp/installs",
        };
    }
}
