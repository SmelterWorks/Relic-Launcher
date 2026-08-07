using FluentAssertions;
using RelicLauncher.Core.Paths;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class PathValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetFullPath_RejectsEmptyPath(string? path)
    {
        var ok = PathValidator.TryGetFullPath(path, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("empty");
    }

    [Fact]
    public void TryGetFullPath_ResolvesRelativePath()
    {
        var ok = PathValidator.TryGetFullPath(".", out var fullPath, out var error);

        ok.Should().BeTrue();
        error.Should().BeEmpty();
        fullPath.Should().NotBeNullOrWhiteSpace();
        Directory.Exists(fullPath).Should().BeTrue();
    }

    [Fact]
    public void TryGetFullPath_NormalizesExistingDirectory()
    {
        using var temp = new TempDirectory();
        var ok = PathValidator.TryGetFullPath(temp.Path, out var fullPath, out _);

        ok.Should().BeTrue();
        fullPath.Should().Be(Path.GetFullPath(temp.Path));
    }

    [Fact]
    public void TryResolveChildPath_RejectsTraversalOutsideRoot()
    {
        using var temp = new TempDirectory();
        var ok = PathValidator.TryResolveChildPath(temp.Path, "../outside.txt", out _);

        ok.Should().BeFalse();
    }

    [Fact]
    public void TryResolveChildPath_AllowsFileInsideRoot()
    {
        using var temp = new TempDirectory();
        var ok = PathValidator.TryResolveChildPath(temp.Path, "child/file.txt", out var destination);

        ok.Should().BeTrue();
        destination.Should().StartWith(Path.GetFullPath(temp.Path));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RelicLauncherTests", Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
