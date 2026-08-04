using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Platform;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class RuntimePlatformTests
{
    [Theory]
    [InlineData(HostOs.Windows, HostArch.X64, "windows")]
    [InlineData(HostOs.Linux, HostArch.X64, "linux")]
    [InlineData(HostOs.MacOs, HostArch.Arm64, "mac-arm64")]
    [InlineData(HostOs.MacOs, HostArch.X64, "mac-x64")]
    public void ResolveClientPackageKey_MapsOsArch(HostOs os, HostArch arch, string expected)
    {
        RuntimePlatform.ResolveClientPackageKey(os, arch).Should().Be(expected);
    }
}
