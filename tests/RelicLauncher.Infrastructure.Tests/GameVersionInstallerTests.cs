using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Versions;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class GameVersionInstallerTests
{
    [Fact]
    public void SelectClientPackage_PrefersPlatformMatch()
    {
        using var temp = new TempAppPaths();
        using var installer = CreateInstaller(temp, new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var version = new GameVersionInfo
        {
            Version = "1.22.6",
            Channel = GameVersionChannel.Stable,
            Packages =
            [
                CreatePackage("windows", ClientPackageKind.WindowsInstaller, "win.exe", "https://cdn.example/win.exe"),
                CreatePackage("linux", ClientPackageKind.TarGz, "linux.tar.gz", "https://cdn.example/linux.tar.gz"),
            ],
        };

        var selected = installer.SelectClientPackage(version, CreatePlatform(HostOs.Linux, "linux"));

        selected!.PlatformKey.Should().Be("linux");
    }

    [Fact]
    public void SelectClientPackage_FallsBackToArchiveKind()
    {
        using var temp = new TempAppPaths();
        using var installer = CreateInstaller(temp, new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var version = new GameVersionInfo
        {
            Version = "1.22.6",
            Channel = GameVersionChannel.Stable,
            Packages =
            [
                CreatePackage("windows", ClientPackageKind.WindowsInstaller, "win.exe", "https://cdn.example/win.exe"),
                CreatePackage("linux", ClientPackageKind.TarGz, "linux.tar.gz", "https://cdn.example/linux.tar.gz"),
            ],
        };

        var selected = installer.SelectClientPackage(version, CreatePlatform(HostOs.MacOs, "mac-arm64"));

        selected!.Kind.Should().Be(ClientPackageKind.TarGz);
    }

    [Fact]
    public async Task InstallAsync_Fails_WhenInstallsRootMissing()
    {
        using var temp = new TempAppPaths();
        using var installer = CreateInstaller(temp, new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var result = await installer.InstallAsync(new VersionInstallRequest
        {
            InstallsRoot = "   ",
            Version = new GameVersionInfo { Version = "1.22.6", Channel = GameVersionChannel.Stable, Packages = [] },
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Installs root");
    }

    [Fact]
    public async Task InstallAsync_Fails_WhenPackageMissing()
    {
        using var temp = new TempAppPaths();
        using var installer = CreateInstaller(
            temp,
            new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)),
            new FakeRuntimePlatform { Info = CreatePlatform(HostOs.Linux, "linux") });
        var result = await installer.InstallAsync(new VersionInstallRequest
        {
            InstallsRoot = Path.Combine(temp.Paths.RootDirectory, "installs"),
            Version = new GameVersionInfo
            {
                Version = "1.22.6",
                Channel = GameVersionChannel.Stable,
                Packages = [CreatePackage("windows", ClientPackageKind.WindowsInstaller, "win.exe", "https://cdn.example/win.exe")],
            },
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No client package");
    }

    [Fact]
    public async Task InstallAsync_DownloadsExtractsAndRecordsInstall()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var archiveBytes = CreateTarGzWithExecutable();
        var md5 = Convert.ToHexString(MD5.HashData(archiveBytes)).ToLowerInvariant();
        var handler = new StubHandler(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "https://cdn.example/primary.tar.gz", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(archiveBytes),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var installer = CreateInstaller(temp, handler, new FakeRuntimePlatform
        {
            Info = CreatePlatform(HostOs.Linux, "linux"),
        });

        var result = await installer.InstallAsync(new VersionInstallRequest
        {
            InstallsRoot = installsRoot,
            Version = new GameVersionInfo
            {
                Version = "1.22.6",
                Channel = GameVersionChannel.Stable,
                Packages =
                [
                    new GameVersionPackage
                    {
                        PlatformKey = "linux",
                        Kind = ClientPackageKind.TarGz,
                        FileName = "vs_client_linux-x64_1.22.6.tar.gz",
                        CdnUrl = "https://cdn.example/primary.tar.gz",
                        LocalUrl = "https://mirror.example/fallback.tar.gz",
                        Md5 = md5,
                    },
                ],
            },
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExecutableFound.Should().BeTrue();
        File.Exists(Path.Combine(installsRoot, "versions.json")).Should().BeTrue();
    }

    [Fact]
    public async Task DownloadAsync_FallsBackToSecondaryUrl()
    {
        using var temp = new TempAppPaths();
        var payload = Encoding.UTF8.GetBytes("archive");
        var calls = new List<string>();
        var handler = new StubHandler(request =>
        {
            calls.Add(request.RequestUri!.ToString());
            if (request.RequestUri.ToString().Contains("primary", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            };
        });

        using var installer = CreateInstaller(temp, handler);
        var destination = Path.Combine(temp.Paths.CacheDirectory, "game.tar.gz");
        Directory.CreateDirectory(temp.Paths.CacheDirectory);
        var result = await installer.DownloadAsync(
            new GameVersionPackage
            {
                PlatformKey = "linux",
                CdnUrl = "https://cdn.example/primary.tar.gz",
                LocalUrl = "https://mirror.example/fallback.tar.gz",
                FileName = "game.tar.gz",
            },
            destination,
            progress: null,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        calls.Should().HaveCount(2);
        File.ReadAllBytes(destination).Should().Equal(payload);
    }

    [Fact]
    public async Task InstallAsync_Fails_WhenMd5Mismatch()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var archiveBytes = CreateTarGzWithExecutable();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(archiveBytes),
        });

        using var installer = CreateInstaller(temp, handler, new FakeRuntimePlatform
        {
            Info = CreatePlatform(HostOs.Linux, "linux"),
        });

        var result = await installer.InstallAsync(new VersionInstallRequest
        {
            InstallsRoot = installsRoot,
            Version = new GameVersionInfo
            {
                Version = "1.22.6",
                Channel = GameVersionChannel.Stable,
                Packages =
                [
                    new GameVersionPackage
                    {
                        PlatformKey = "linux",
                        Kind = ClientPackageKind.TarGz,
                        FileName = "vs_client_linux-x64_1.22.6.tar.gz",
                        CdnUrl = "https://cdn.example/linux.tar.gz",
                        Md5 = "deadbeef",
                    },
                ],
            },
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("MD5");
    }

    [Fact]
    public async Task UninstallAsync_RemovesVersionDirectoryAndInventoryEntry()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var versionDir = Path.Combine(installsRoot, "versions", "1.22.6");
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(Path.Combine(versionDir, "Vintagestory"), "bin");

        var store = new JsonInstalledVersionStore();
        await store.SaveAsync(installsRoot,
        [
            new InstalledGameVersion
            {
                Version = "1.22.6",
                InstallPath = versionDir,
                ExecutablePath = Path.Combine(versionDir, "Vintagestory"),
                ExecutableFound = true,
            },
        ]);

        using var installer = CreateInstaller(temp, new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var result = await installer.UninstallAsync(installsRoot, "1.22.6");

        result.IsSuccess.Should().BeTrue();
        Directory.Exists(versionDir).Should().BeFalse();
        (await store.ListAsync(installsRoot)).Value.Should().BeEmpty();
    }

    private static GameVersionInstaller CreateInstaller(
        TempAppPaths temp,
        StubHandler handler,
        FakeRuntimePlatform? platform = null)
        => new(
            new FixedPathProvider(temp.Paths),
            new JsonInstalledVersionStore(),
            platform ?? new FakeRuntimePlatform(),
            NullLogger<GameVersionInstaller>.Instance,
            new HttpClient(handler));

    private static GameVersionPackage CreatePackage(
        string platformKey,
        ClientPackageKind kind,
        string fileName,
        string cdnUrl)
        => new()
        {
            PlatformKey = platformKey,
            Kind = kind,
            FileName = fileName,
            CdnUrl = cdnUrl,
        };

    private static PlatformInfo CreatePlatform(HostOs os, string packageKey)
        => new()
        {
            Os = os,
            Arch = HostArch.X64,
            ClientPackageKey = packageKey,
            DefaultDataPath = "/tmp/data",
            DefaultInstallsRoot = "/tmp/installs",
        };

    private static byte[] CreateTarGzWithExecutable()
    {
        using var tarBuffer = new MemoryStream();
        using (var tarWriter = new TarWriter(tarBuffer, leaveOpen: true))
        {
            var entry = new V7TarEntry(TarEntryType.V7RegularFile, "Vintagestory")
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes("bin")),
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
