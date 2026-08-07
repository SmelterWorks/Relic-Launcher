using FluentAssertions;
using RelicLauncher.Infrastructure.SelfCheck;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class RelicSelfCheckRunnerTests
{
    [Fact]
    public async Task RunAsync_PassesCoreChecks_WithIsolatedDataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "RelicLauncherTests", Guid.NewGuid().ToString("N"));
        var runner = new RelicSelfCheckRunner(root, includeNetwork: false);

        var report = await runner.RunAsync();

        report.Passed.Should().BeTrue();
        report.FailCount.Should().Be(0);
        report.Items.Should().Contain(static item => item.Id == "version" && item.Status == Core.SelfCheck.SelfCheckStatus.Pass);
        report.Items.Should().Contain(static item => item.Id == "server-layout" && item.Status == Core.SelfCheck.SelfCheckStatus.Pass);
        report.Items.Should().Contain(static item => item.Id == "catalog" && item.Status == Core.SelfCheck.SelfCheckStatus.Skip);

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveDataRoot_UsesOverridePath()
    {
        var path = Path.Combine(Path.GetTempPath(), "relic-override");
        SelfCheckEnvironment.ResolveDataRoot(path).Should().Be(Path.GetFullPath(path));
    }
}
