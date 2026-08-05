using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Versions;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class VintageStoryVersionCatalogTests
{
    [Fact]
    public void ParseCatalog_ExtractsClientPackages_AndSkipsServers()
    {
        var json = """
            {
              "1.22.6": {
                "windows": {
                  "filename": "vs_install_win-x64_1.22.6.exe",
                  "md5": "abc",
                  "urls": { "cdn": "https://cdn.example/win.exe" },
                  "latest": 1
                },
                "linux": {
                  "filename": "vs_client_linux-x64_1.22.6.tar.gz",
                  "urls": { "cdn": "https://cdn.example/linux.tar.gz" }
                },
                "linuxserver": {
                  "filename": "vs_server_linux-x64_1.22.6.tar.gz",
                  "urls": { "cdn": "https://cdn.example/server.tar.gz" }
                }
              }
            }
            """;

        var versions = VintageStoryVersionCatalog.ParseCatalog(json);

        versions.Should().ContainSingle();
        versions[0].Version.Should().Be("1.22.6");
        versions[0].IsLatest.Should().BeTrue();
        versions[0].Packages.Should().HaveCount(2);
        versions[0].Packages.Should().Contain(p => p.PlatformKey == "linux" && p.Kind == ClientPackageKind.TarGz);
        versions[0].Packages.Should().Contain(p => p.PlatformKey == "windows" && p.Kind == ClientPackageKind.WindowsInstaller);
    }

    [Fact]
    public void ParseCatalog_SkipsEntriesWithoutUrls()
    {
        var json = """
            {
              "1.22.6": {
                "linux": {
                  "filename": "vs_client_linux-x64_1.22.6.tar.gz"
                }
              }
            }
            """;

        var versions = VintageStoryVersionCatalog.ParseCatalog(json);

        versions.Should().BeEmpty();
    }

    [Fact]
    public void ParseCatalog_ParsesZipAndLocalUrl()
    {
        var json = """
            {
              "1.21.0": {
                "mac-arm64": {
                  "filename": "vs_client_mac-arm64_1.21.0.zip",
                  "urls": {
                    "cdn": "https://cdn.example/mac.zip",
                    "local": "https://mirror.example/mac.zip"
                  },
                  "md5": "abc123"
                }
              }
            }
            """;

        var versions = VintageStoryVersionCatalog.ParseCatalog(json);

        versions.Should().ContainSingle();
        versions[0].Packages.Should().ContainSingle(p =>
            p.PlatformKey == "mac-arm64" &&
            p.Kind == ClientPackageKind.Zip &&
            p.LocalUrl == "https://mirror.example/mac.zip" &&
            p.Md5 == "abc123");
    }
}
