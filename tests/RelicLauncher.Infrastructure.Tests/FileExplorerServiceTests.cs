using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Infrastructure.Paths;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class FileExplorerServiceTests
{
    private readonly FileExplorerService _service = new(NullLogger<FileExplorerService>.Instance);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenFolder_Fails_WhenPathBlank(string? path)
    {
        var result = _service.OpenFolder(path!);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void OpenFolder_Fails_WhenFolderMissing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "RelicLauncherTests", Guid.NewGuid().ToString("N"));

        var result = _service.OpenFolder(missing);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("does not exist");
    }

    [Fact]
    public void OpenFolder_Succeeds_ForExistingDirectory()
    {
        using var temp = new TempAppPaths();
        Directory.CreateDirectory(temp.Paths.LogsDirectory);

        var result = _service.OpenFolder(temp.Paths.LogsDirectory);

        result.IsSuccess.Should().BeTrue();
    }
}
