using FluentAssertions;
using RelicLauncher.Core.Paths;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class VintageStoryServerExecutableLocatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FindServerExecutable_ReturnsNull_WhenInstallPathMissing(string? installPath)
    {
        VintageStoryServerExecutableLocator.FindServerExecutable(installPath!).Should().BeNull();
    }

    [Fact]
    public void FindServerExecutable_PrefersExtensionlessBinary()
    {
        using var install = new TempInstall();
        var scriptPath = Path.Combine(install.Path, "VintagestoryServer");
        File.WriteAllText(scriptPath, "#!/bin/sh\n");
        File.WriteAllText(Path.Combine(install.Path, "VintagestoryServer.dll"), "dll");

        VintageStoryServerExecutableLocator.FindServerExecutable(install.Path).Should().Be(scriptPath);
    }

    [Fact]
    public void FindServerExecutable_FindsExeOnWindowsStyleLayout()
    {
        using var install = new TempInstall();
        var exePath = Path.Combine(install.Path, "VintagestoryServer.exe");
        File.WriteAllText(exePath, "exe");

        VintageStoryServerExecutableLocator.FindServerExecutable(install.Path).Should().Be(exePath);
    }

    [Fact]
    public void FindServerExecutable_FindsDllWhenOnlyCandidate()
    {
        using var install = new TempInstall();
        var dllPath = Path.Combine(install.Path, "VintagestoryServer.dll");
        File.WriteAllText(dllPath, "dll");

        VintageStoryServerExecutableLocator.FindServerExecutable(install.Path).Should().Be(dllPath);
    }

    private sealed class TempInstall : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RelicLauncherTests", Guid.NewGuid().ToString("N"));

        public TempInstall() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
