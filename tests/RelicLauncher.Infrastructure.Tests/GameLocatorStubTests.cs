using FluentAssertions;
using RelicLauncher.Infrastructure.Stubs;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class GameLocatorStubTests
{
    private readonly GameLocatorStub _locator = new();

    [Fact]
    public async Task LocateAsync_Fails_WhenPathNotConfigured()
    {
        var result = await _locator.LocateAsync(null);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("install path");
    }

    [Fact]
    public async Task LocateAsync_Fails_WhenDirectoryMissing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "RelicLauncherTests", Guid.NewGuid().ToString("N"));

        var result = await _locator.LocateAsync(missing);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("does not exist");
    }

    [Fact]
    public async Task LocateAsync_Succeeds_WithExecutablePath()
    {
        using var temp = new TempAppPaths();
        var executable = Path.Combine(temp.Paths.RootDirectory, "Vintagestory");
        File.WriteAllText(executable, "client");

        var result = await _locator.LocateAsync(temp.Paths.RootDirectory);

        result.IsSuccess.Should().BeTrue();
        result.Value!.InstallPath.Should().Be(Path.GetFullPath(temp.Paths.RootDirectory));
        result.Value.ExecutableFound.Should().BeTrue();
        result.Value.ExecutablePath.Should().Be(executable);
    }

    [Fact]
    public async Task LocateAsync_Succeeds_WithoutExecutable()
    {
        using var temp = new TempAppPaths();

        var result = await _locator.LocateAsync(temp.Paths.RootDirectory);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExecutableFound.Should().BeFalse();
        result.Value.ExecutablePath.Should().BeNull();
    }
}
