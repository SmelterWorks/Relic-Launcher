using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.DotNet;
using RelicLauncher.Infrastructure.Server;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class GameServerHostTests
{
    [Fact]
    public async Task StartAsync_Fails_WhenVersionNotInstalled()
    {
        using var host = CreateHost();
        var result = await host.StartAsync(new GameServerStartRequest
        {
            InstallsRoot = Path.GetTempPath(),
            Version = "9.9.9",
            ServerDataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not installed");
        host.State.Should().Be(ServerProcessState.Stopped);
    }

    [Fact]
    public async Task SendCommandAsync_Fails_WhenServerStopped()
    {
        using var host = CreateHost();
        var result = await host.SendCommandAsync("help");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ClearOutput_RemovesLines()
    {
        using var host = CreateHost();
        host.ClearOutput();
        host.OutputLines.Should().BeEmpty();
    }

    private static GameServerHost CreateHost()
    {
        using var paths = new TempAppPaths();
        var platform = new FakeRuntimePlatform();
        var provisioner = new DotNetRuntimeProvisioner(
            new FixedPathProvider(paths.Paths),
            platform,
            NullLogger<DotNetRuntimeProvisioner>.Instance);
        return new GameServerHost(provisioner, NullLogger<GameServerHost>.Instance);
    }
}
