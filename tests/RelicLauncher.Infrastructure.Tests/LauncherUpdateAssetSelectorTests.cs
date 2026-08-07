using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Updates;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class LauncherUpdateAssetSelectorTests
{
    [Fact]
    public void Select_PrefersMatchingRid()
    {
        var selector = new LauncherUpdateAssetSelector();
        var update = new LauncherUpdateInfo
        {
            Version = "1.0.0",
            ReleaseNotesUrl = "https://example.test",
            Channel = LauncherUpdateChannel.Stable,
            Assets =
            [
                new LauncherUpdateAsset
                {
                    InstallKind = "LinuxAppImage",
                    Rid = "linux-arm64",
                    Filename = "arm.AppImage",
                    Url = "https://smelterworks.com/files/relic/1.0/arm.AppImage",
                    Sha256 = "a",
                    SizeBytes = 1,
                },
                new LauncherUpdateAsset
                {
                    InstallKind = "LinuxAppImage",
                    Rid = "linux-x64",
                    Filename = "x64.AppImage",
                    Url = "https://smelterworks.com/files/relic/1.0/x64.AppImage",
                    Sha256 = "b",
                    SizeBytes = 1,
                },
            ],
        };

        var install = new DetectedLauncherInstall
        {
            InstallKind = LauncherInstallKind.LinuxAppImage,
            Rid = "linux-x64",
            CanApplyInApp = true,
        };

        selector.Select(update, install)!.Filename.Should().Be("x64.AppImage");
    }
}
