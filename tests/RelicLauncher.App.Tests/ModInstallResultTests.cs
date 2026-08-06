using FluentAssertions;
using RelicLauncher.App.Services;
using Xunit;

namespace RelicLauncher.App.Tests;

public class ModInstallResultTests
{
    [Fact]
    public void Factories_SetExpectedFlags()
    {
        ModInstallResult.Ok("done").Success.Should().BeTrue();
        ModInstallResult.Ok("done").Message.Should().Be("done");

        ModInstallResult.Fail("broken").Success.Should().BeFalse();
        ModInstallResult.Fail("broken").Canceled.Should().BeFalse();

        ModInstallResult.Cancel().Success.Should().BeFalse();
        ModInstallResult.Cancel().Canceled.Should().BeTrue();
    }
}
