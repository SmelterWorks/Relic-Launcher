using FluentAssertions;
using RelicLauncher.Infrastructure.Updates;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class LauncherVersionComparerTests
{
    [Theory]
    [InlineData("0.1.0", "0.2.0", true)]
    [InlineData("0.2.0", "0.1.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("v0.1.0", "0.2.0", true)]
    public void IsUpdateAvailable_ComparesSemver(string current, string remote, bool expected)
    {
        LauncherVersionComparer.IsUpdateAvailable(current, remote).Should().Be(expected);
    }
}
