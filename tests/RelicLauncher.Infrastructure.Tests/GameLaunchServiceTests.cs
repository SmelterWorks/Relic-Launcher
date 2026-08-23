using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;
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

        var service = CreateService(new CapturingSandboxBrokerClient());
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

        var broker = new CapturingSandboxBrokerClient();
        var service = CreateService(broker, new StubRuntimeProvisioner { IsManagedByRelic = false });
        var result = await service.LaunchAsync(new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = "1.22.6",
            DataPath = dataPath,
        });

        result.IsSuccess.Should().BeTrue();
        broker.LastExecutable.Should().Be(exePath);
        broker.LastArguments.Should().Equal("--dataPath", dataPath);
        broker.LastEnvironment.Should().BeEmpty();
        service.IsRunning.Should().BeTrue();
        service.RunningVersion.Should().Be("1.22.6");
        Directory.Exists(Path.Combine(dataPath, "Mods")).Should().BeTrue();
    }

    [Fact]
    public async Task LaunchAsync_PassesConnectArguments()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var versionDir = Path.Combine(installsRoot, "versions", "1.22.6");
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(Path.Combine(versionDir, "Vintagestory"), "bin");
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");

        var broker = new CapturingSandboxBrokerClient();
        var service = CreateService(broker, new StubRuntimeProvisioner { IsManagedByRelic = false });
        var result = await service.LaunchAsync(new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = "1.22.6",
            DataPath = dataPath,
            ConnectAddress = "tops.vintagestory.at:42420",
            ConnectPassword = "secret",
        });

        result.IsSuccess.Should().BeTrue();
        broker.LastArguments.Should().Equal(
            "--dataPath", dataPath,
            "--connect", "tops.vintagestory.at:42420",
            "--pw", "secret");
    }

    [Fact]
    public async Task LaunchAsync_SetsDotNetRoot_WhenRuntimeIsManaged()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var versionDir = Path.Combine(installsRoot, "versions", "1.21.5");
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(Path.Combine(versionDir, "Vintagestory"), "bin");
        var managedRoot = Path.Combine(temp.Paths.CacheDirectory, "dotnet", "net8");

        var broker = new CapturingSandboxBrokerClient();
        var service = CreateService(broker, new StubRuntimeProvisioner
        {
            IsManagedByRelic = true,
            DotNetRoot = managedRoot,
            MajorVersion = 8,
        });
        var result = await service.LaunchAsync(new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = "1.21.5",
            DataPath = Path.Combine(temp.Paths.RootDirectory, "data"),
        });

        result.IsSuccess.Should().BeTrue();
        broker.LastEnvironment.Should().ContainKey("DOTNET_ROOT");
        broker.LastEnvironment["DOTNET_ROOT"].Should().Be(managedRoot);
    }

    [Fact]
    public async Task LaunchAsync_Fails_WhenRuntimeProvisionFails()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var versionDir = Path.Combine(installsRoot, "versions", "1.22.6");
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(Path.Combine(versionDir, "Vintagestory"), "bin");

        var service = CreateService(
            new CapturingSandboxBrokerClient(),
            new StubRuntimeProvisioner { FailWith = "download blocked" });
        var result = await service.LaunchAsync(new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = "1.22.6",
            DataPath = Path.Combine(temp.Paths.RootDirectory, "data"),
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("download blocked");
        service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task LaunchAsync_Fails_WhenAlreadyRunning()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var versionDir = Path.Combine(installsRoot, "versions", "1.22.6");
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(Path.Combine(versionDir, "Vintagestory"), "bin");

        var broker = new CapturingSandboxBrokerClient();
        var service = CreateService(broker, new StubRuntimeProvisioner { IsManagedByRelic = false });
        var request = new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = "1.22.6",
            DataPath = Path.Combine(temp.Paths.RootDirectory, "data"),
        };

        (await service.LaunchAsync(request)).IsSuccess.Should().BeTrue();
        var second = await service.LaunchAsync(request);
        second.IsSuccess.Should().BeFalse();
        second.Error.Should().Contain("already running");
    }

    [Fact]
    public async Task StopAsync_ClearsRunningState()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var versionDir = Path.Combine(installsRoot, "versions", "1.22.6");
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(Path.Combine(versionDir, "Vintagestory"), "bin");

        var broker = new CapturingSandboxBrokerClient();
        var service = CreateService(broker, new StubRuntimeProvisioner { IsManagedByRelic = false });
        var launch = await service.LaunchAsync(new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = "1.22.6",
            DataPath = Path.Combine(temp.Paths.RootDirectory, "data"),
        });

        launch.IsSuccess.Should().BeTrue();
        service.IsRunning.Should().BeTrue();

        var stop = await service.StopAsync();
        stop.IsSuccess.Should().BeTrue();
        service.IsRunning.Should().BeFalse();
        service.RunningVersion.Should().BeNull();
        broker.KilledProcessIds.Should().Contain(broker.LastProcessId);
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
        var service = new GameLaunchService(
            new CapturingSandboxBrokerClient(),
            new RuntimePlatform(),
            writer,
            new StubRuntimeProvisioner { IsManagedByRelic = false },
            auth,
            NullLogger<GameLaunchService>.Instance);
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

    [Fact]
    public async Task LaunchAsync_Fails_WhenNotSignedIn()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var versionDir = Path.Combine(installsRoot, "versions", "1.22.6");
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(Path.Combine(versionDir, "Vintagestory"), "bin");

        var service = new GameLaunchService(
            new CapturingSandboxBrokerClient(),
            new RuntimePlatform(),
            new NoopSessionWriter(),
            new StubRuntimeProvisioner(),
            new StubAccountAuth { ValidateFailure = "Sign in with your Vintage Story game account in Settings." },
            NullLogger<GameLaunchService>.Instance);

        var result = await service.LaunchAsync(new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = "1.22.6",
            DataPath = Path.Combine(temp.Paths.RootDirectory, "data"),
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Sign in");
    }

    [Fact]
    public async Task LaunchAsync_Fails_WhenSessionWriteFails()
    {
        using var temp = new TempAppPaths();
        var installsRoot = Path.Combine(temp.Paths.RootDirectory, "installs");
        var versionDir = Path.Combine(installsRoot, "versions", "1.22.6");
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(Path.Combine(versionDir, "Vintagestory"), "bin");

        var service = new GameLaunchService(
            new CapturingSandboxBrokerClient(),
            new RuntimePlatform(),
            new FailingSessionWriter(),
            new StubRuntimeProvisioner { IsManagedByRelic = false },
            new StubAccountAuth { Status = new AccountSessionStatus { IsSignedIn = true, PlayerUid = "uid-1" } },
            NullLogger<GameLaunchService>.Instance);

        var result = await service.LaunchAsync(new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = "1.22.6",
            DataPath = Path.Combine(temp.Paths.RootDirectory, "data"),
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("clientsettings.json");
    }

    private static GameLaunchService CreateService(
        CapturingSandboxBrokerClient broker,
        StubRuntimeProvisioner? provisioner = null)
        => new(
            broker,
            new RuntimePlatform(),
            new NoopSessionWriter(),
            provisioner ?? new StubRuntimeProvisioner { IsManagedByRelic = true },
            new StubAccountAuth { Status = new AccountSessionStatus { IsSignedIn = true, PlayerUid = "uid-1" } },
            NullLogger<GameLaunchService>.Instance);

    private sealed class NoopSessionWriter : IClientSettingsSessionWriter
    {
        public Task<Result> ApplySessionAsync(string dataPath, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> ClearSessionAsync(string dataPath, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class FailingSessionWriter : IClientSettingsSessionWriter
    {
        public Task<Result> ApplySessionAsync(string dataPath, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure("Could not write clientsettings.json."));

        public Task<Result> ClearSessionAsync(string dataPath, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class StubAccountAuth : IAccountAuthService
    {
        public AccountSessionStatus Status { get; init; } = new() { IsSignedIn = false };
        public string? ValidateFailure { get; init; }

        public Task<Result<AccountSessionStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AccountSessionStatus>.Success(Status));

        public Task<Result<AccountSessionStatus>> LoginAsync(AccountCredentials credentials, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> ValidateSessionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ValidateFailure is null ? Result.Success() : Result.Failure(ValidateFailure));
    }

    private sealed class StubRuntimeProvisioner : IDotNetRuntimeProvisioner
    {
        public bool IsManagedByRelic { get; init; } = true;
        public string DotNetRoot { get; init; } = "/managed/dotnet";
        public int MajorVersion { get; init; } = 10;
        public string? FailWith { get; init; }
        public int EnsureCallCount { get; private set; }

        public Task<Result<DotNetRuntimeResolveInfo>> EnsureAsync(
            int majorVersion,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            EnsureCallCount++;
            progress?.Report(1.0);
            if (FailWith is not null)
            {
                return Task.FromResult(Result<DotNetRuntimeResolveInfo>.Failure(FailWith));
            }

            return Task.FromResult(Result<DotNetRuntimeResolveInfo>.Success(new DotNetRuntimeResolveInfo
            {
                DotNetRoot = DotNetRoot,
                IsManagedByRelic = IsManagedByRelic,
                MajorVersion = MajorVersion == 0 ? majorVersion : MajorVersion,
            }));
        }
    }

    private sealed class CapturingSandboxBrokerClient : ISandboxBrokerClient
    {
        public string? LastExecutable { get; private set; }
        public IReadOnlyList<string> LastArguments { get; private set; } = [];
        public IReadOnlyDictionary<string, string?> LastEnvironment { get; private set; } =
            new Dictionary<string, string?>(StringComparer.Ordinal);
        public int LastProcessId { get; private set; }
        public List<int> KilledProcessIds { get; } = [];

        public Task<Result<SandboxLaunchResult>> LaunchSandboxedAsync(
            SandboxLaunchRequest request,
            CancellationToken cancellationToken = default)
        {
            LastExecutable = request.ExecutablePath;
            LastArguments = request.Arguments.ToList();
            LastEnvironment = new Dictionary<string, string?>(request.Environment, StringComparer.Ordinal);
            LastProcessId = Environment.ProcessId;
            return Task.FromResult(Result<SandboxLaunchResult>.Success(new SandboxLaunchResult
            {
                ProcessId = LastProcessId,
                Sandboxed = false,
            }));
        }

        public Task<Result> OpenDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> OpenUrlAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> RunInstallerAsync(
            string installerPath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> WriteFileAsync(
            string destinationPath,
            byte[] content,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<byte[]> ReadProcessOutputAsync(int processId, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<Result> WriteProcessInputAsync(
            int processId,
            string text,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> KillProcessAsync(int processId, CancellationToken cancellationToken = default)
        {
            KilledProcessIds.Add(processId);
            return Task.FromResult(Result.Success());
        }
    }
}
