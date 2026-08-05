using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.DotNet;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class DotNetRuntimeLayoutTests
{
    [Fact]
    public void GetManagedRoot_UsesMajorVersionFolder()
    {
        using var temp = new TempAppPaths();
        DotNetRuntimeLayout.GetManagedRoot(temp.Paths.CacheDirectory, 8)
            .Should().Be(Path.Combine(temp.Paths.CacheDirectory, "dotnet", "net8"));
    }

    [Fact]
    public void HasRequiredSharedFrameworks_RequiresDesktopOnWindows()
    {
        using var temp = new TempAppPaths();
        var root = Path.Combine(temp.Paths.RootDirectory, "dotnet");
        SeedSharedFramework(root, major: 8, includeDesktop: false);

        DotNetRuntimeLayout.HasRequiredSharedFrameworks(root, 8, requireWindowsDesktop: false).Should().BeTrue();
        DotNetRuntimeLayout.HasRequiredSharedFrameworks(root, 8, requireWindowsDesktop: true).Should().BeFalse();
    }

    [Fact]
    public void HasMajorSharedFramework_MatchesPatchVersions()
    {
        using var temp = new TempAppPaths();
        var shared = Path.Combine(temp.Paths.RootDirectory, "shared", "Microsoft.NETCore.App");
        Directory.CreateDirectory(Path.Combine(shared, "8.0.29"));
        Directory.CreateDirectory(Path.Combine(shared, "7.0.20"));

        DotNetRuntimeLayout.HasMajorSharedFramework(shared, 8).Should().BeTrue();
        DotNetRuntimeLayout.HasMajorSharedFramework(shared, 10).Should().BeFalse();
    }

    [Theory]
    [InlineData(HostOs.Windows, HostArch.X64, "win-x64")]
    [InlineData(HostOs.Linux, HostArch.X64, "linux-x64")]
    [InlineData(HostOs.MacOs, HostArch.Arm64, "osx-arm64")]
    [InlineData(HostOs.Linux, HostArch.Arm64, null)]
    public void TryMap_ResolvesKnownRids(HostOs os, HostArch arch, string? expected)
        => DotNetRidMapper.TryMap(os, arch).Should().Be(expected);

    [Theory]
    [InlineData(HostOs.Windows, true)]
    [InlineData(HostOs.Linux, false)]
    public void RequiresWindowsDesktop_OnlyOnWindows(HostOs os, bool expected)
        => DotNetRidMapper.RequiresWindowsDesktop(os).Should().Be(expected);

    private static void SeedSharedFramework(string root, int major, bool includeDesktop)
    {
        var version = $"{major}.0.0";
        Directory.CreateDirectory(Path.Combine(root, "shared", "Microsoft.NETCore.App", version));
        if (includeDesktop)
        {
            Directory.CreateDirectory(Path.Combine(root, "shared", "Microsoft.WindowsDesktop.App", version));
        }
    }
}
