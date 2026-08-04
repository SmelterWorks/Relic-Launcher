using FluentAssertions;
using RelicLauncher.Core.Paths;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class VintageStoryExecutableLocatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FindClientExecutable_ReturnsNull_WhenInstallPathMissing(string? installPath)
    {
        VintageStoryExecutableLocator.FindClientExecutable(installPath!).Should().BeNull();
    }

    [Fact]
    public void FindClientExecutable_PrefersExtensionlessBinary()
    {
        using var install = new TempInstall();
        var scriptPath = Path.Combine(install.Path, "Vintagestory");
        File.WriteAllText(scriptPath, "#!/bin/sh\n");
        File.WriteAllText(Path.Combine(install.Path, "Vintagestory.dll"), "dll");

        VintageStoryExecutableLocator.FindClientExecutable(install.Path).Should().Be(scriptPath);
    }

    [Fact]
    public void FindClientExecutable_FindsExeOnWindowsStyleLayout()
    {
        using var install = new TempInstall();
        var exePath = Path.Combine(install.Path, "Vintagestory.exe");
        File.WriteAllText(exePath, "exe");

        VintageStoryExecutableLocator.FindClientExecutable(install.Path).Should().Be(exePath);
    }

    [Fact]
    public void FindClientExecutable_FindsDllWhenOnlyCandidate()
    {
        using var install = new TempInstall();
        var dllPath = Path.Combine(install.Path, "Vintagestory.dll");
        File.WriteAllText(dllPath, "dll");

        VintageStoryExecutableLocator.FindClientExecutable(install.Path).Should().Be(dllPath);
    }

    [Fact]
    public void FindClientExecutable_ReturnsNull_WhenDirectoryEmpty()
    {
        using var install = new TempInstall();
        VintageStoryExecutableLocator.FindClientExecutable(install.Path).Should().BeNull();
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
