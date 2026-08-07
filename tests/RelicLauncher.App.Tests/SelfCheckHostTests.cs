using FluentAssertions;
using RelicLauncher.App.SelfCheck;
using RelicLauncher.Core.SelfCheck;
using Xunit;

namespace RelicLauncher.App.Tests;

public class SelfCheckHostTests
{
    [Fact]
    public void TryHandle_ReturnsFalse_WhenFlagMissing()
    {
        SelfCheckHost.TryHandle(["--help"], out var exitCode).Should().BeFalse();
        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_IncludesNavigationCheck()
    {
        var root = Path.Combine(Path.GetTempPath(), "RelicLauncherTests", Guid.NewGuid().ToString("N"));
        var exitCode = await SelfCheckHost.RunAsync(
            ["--self-check", "--no-network", "--self-check-data", root]);

        exitCode.Should().Be(0);
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ViewNavigationSelfCheck_MapsAllPrimaryPages()
    {
        var item = ViewNavigationSelfCheck.Verify();
        item.Status.Should().Be(SelfCheckStatus.Pass);
        item.Detail.Should().Contain("8 routes");
    }
}
