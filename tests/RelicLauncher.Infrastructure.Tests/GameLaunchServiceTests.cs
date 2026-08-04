using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Launch;
using RelicLauncher.Infrastructure.Platform;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class GameLaunchServiceTests
{
    [Fact]
    public async Task ResolveAsync_FindsExecutable_InVersionFolder()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var versionDir = Path.Combine(installsRoot, "versions", "1.22.6");
        Directory.CreateDirectory(versionDir);
        var exePath = Path.Combine(versionDir, "Vintagestory");
        await File.WriteAllTextAsync(exePath, "bin");

        var service = new GameLaunchService(new CapturingProcessRunner(), new RuntimePlatform());
        var result = await service.ResolveAsync(new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = "1.22.6",
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExecutableFound.Should().BeTrue();
        result.Value.ExecutablePath.Should().Be(exePath);
    }

    [Fact]
    public async Task LaunchAsync_PassesDataPathArgument()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var versionDir = Path.Combine(installsRoot, "versions", "1.22.6");
        Directory.CreateDirectory(versionDir);
        var exePath = Path.Combine(versionDir, "Vintagestory");
        await File.WriteAllTextAsync(exePath, "bin");
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");

        var runner = new CapturingProcessRunner();
        var service = new GameLaunchService(runner, new RuntimePlatform());
        var result = await service.LaunchAsync(new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = "1.22.6",
            DataPath = dataPath,
        });

        result.IsSuccess.Should().BeTrue();
        runner.LastExecutable.Should().Be(exePath);
        runner.LastArguments.Should().Equal("--dataPath", dataPath);
        Directory.Exists(Path.Combine(dataPath, "Mods")).Should().BeTrue();
    }

    private sealed class CapturingProcessRunner : IProcessRunner
    {
        public string? LastExecutable { get; private set; }
        public IReadOnlyList<string> LastArguments { get; private set; } = [];

        public Task<Result> StartAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            LastExecutable = executablePath;
            LastArguments = arguments.ToList();
            return Task.FromResult(Result.Success());
        }
    }
}
