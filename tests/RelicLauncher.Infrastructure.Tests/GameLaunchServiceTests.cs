using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Auth;
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

        var service = new GameLaunchService(new CapturingProcessRunner(), new RuntimePlatform(), new NoopSessionWriter());
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
        var service = new GameLaunchService(runner, new RuntimePlatform(), new NoopSessionWriter());
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

    [Fact]
    public async Task LaunchAsync_WritesClientSettings_WhenSignedIn()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var versionDir = Path.Combine(installsRoot, "versions", "1.22.6");
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(Path.Combine(versionDir, "Vintagestory"), "bin");
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");

        var auth = new StubAccountAuth
        {
            Status = new AccountSessionStatus
            {
                IsSignedIn = true,
                Email = "player@example.com",
                PlayerName = "PlayerOne",
                PlayerUid = "uid-1",
                SessionKey = "sk",
                SessionSignature = "sig",
                Entitlements = "ent",
                MpToken = "mp",
                HostGameServer = "false",
            },
        };
        var writer = new ClientSettingsSessionWriter(auth, NullLogger<ClientSettingsSessionWriter>.Instance);
        var service = new GameLaunchService(new CapturingProcessRunner(), new RuntimePlatform(), writer);
        var result = await service.LaunchAsync(new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = "1.22.6",
            DataPath = dataPath,
        });

        result.IsSuccess.Should().BeTrue();
        var settingsPath = Path.Combine(dataPath, "clientsettings.json");
        File.Exists(settingsPath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(settingsPath);
        json.Should().Contain("\"sessionkey\": \"sk\"");
        json.Should().Contain("\"playername\": \"PlayerOne\"");
    }

    private sealed class NoopSessionWriter : IClientSettingsSessionWriter
    {
        public Task<Result> ApplySessionAsync(string dataPath, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class StubAccountAuth : IAccountAuthService
    {
        public AccountSessionStatus Status { get; init; } = new() { IsSignedIn = false };

        public Task<Result<AccountSessionStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AccountSessionStatus>.Success(Status));

        public Task<Result<AccountSessionStatus>> LoginAsync(AccountCredentials credentials, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
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
