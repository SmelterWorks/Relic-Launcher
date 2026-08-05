using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Auth;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ClientSettingsSessionWriterTests
{
    [Fact]
    public async Task ApplySessionAsync_Fails_WhenDataPathBlank()
    {
        var writer = new ClientSettingsSessionWriter(new StubAccountAuth(), NullLogger<ClientSettingsSessionWriter>.Instance);
        var result = await writer.ApplySessionAsync("   ");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Data path");
    }

    [Fact]
    public async Task ApplySessionAsync_NoOp_WhenSignedOut()
    {
        using var temp = new TempAppPaths();
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        var writer = new ClientSettingsSessionWriter(
            new StubAccountAuth { Status = Result<AccountSessionStatus>.Success(new AccountSessionStatus { IsSignedIn = false }) },
            NullLogger<ClientSettingsSessionWriter>.Instance);

        var result = await writer.ApplySessionAsync(dataPath);

        result.IsSuccess.Should().BeTrue();
        File.Exists(Path.Combine(dataPath, "clientsettings.json")).Should().BeFalse();
    }

    [Fact]
    public async Task ApplySessionAsync_WritesSessionFields_WhenSignedIn()
    {
        using var temp = new TempAppPaths();
        var dataPath = Path.Combine(temp.Paths.RootDirectory, "data");
        var writer = new ClientSettingsSessionWriter(
            new StubAccountAuth
            {
                Status = Result<AccountSessionStatus>.Success(new AccountSessionStatus
                {
                    IsSignedIn = true,
                    SessionKey = "session-key",
                    SessionSignature = "signature",
                    PlayerUid = "player-uid",
                    PlayerName = "Player",
                    Email = "player@example.test",
                    Entitlements = "entitlements",
                    MpToken = "mp-token",
                    HostGameServer = "host",
                }),
            },
            NullLogger<ClientSettingsSessionWriter>.Instance);

        var result = await writer.ApplySessionAsync(dataPath);

        result.IsSuccess.Should().BeTrue();
        var json = await File.ReadAllTextAsync(Path.Combine(dataPath, "clientsettings.json"));
        json.Should().Contain("session-key");
        json.Should().Contain("player-uid");
        json.Should().Contain("player@example.test");
    }

    private sealed class StubAccountAuth : IAccountAuthService
    {
        public Result<AccountSessionStatus> Status { get; set; } = Result<AccountSessionStatus>.Success(new AccountSessionStatus());

        public Task<Result<AccountSessionStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Status);

        public Task<Result<AccountSessionStatus>> LoginAsync(AccountCredentials credentials, CancellationToken cancellationToken = default)
            => Task.FromResult(Status);

        public Task<Result> LogoutAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }
}
