using FluentAssertions;
using RelicLauncher.App.Services;
using Xunit;

namespace RelicLauncher.App.Tests;

public class LinuxEmbeddedBrowserSupportTests
{
    [Fact]
    public void IsLikelyAvailable_ReturnsTrue_OnNonLinux()
    {
        if (OperatingSystem.IsLinux())
        {
            return;
        }

        LinuxEmbeddedBrowserSupport.IsLikelyAvailable().Should().BeTrue();
    }
}
