using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Auth;
using RelicLauncher.Infrastructure.Security;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class AccountAuthServiceTests
{
    [Fact]
    public async Task LoginAsync_PersistsSession_WhenAuthServerReturnsValid()
    {
        using var temp = new TempAppPaths();
        var secrets = new FileSecretStore(new FixedPathProvider(temp.Paths));
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("latestunstable", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("1.22.0"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"valid":1,"sessionkey":"sk","sessionsignature":"sig","uid":"u1","playername":"PlayerOne","entitlements":"e1","mptoken":"mp","hasgameserver":false}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var auth = new AccountAuthService(secrets, NullLogger<AccountAuthService>.Instance, handler);

        var result = await auth.LoginAsync(new AccountCredentials
        {
            Email = "player@example.com",
            Password = "secret",
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSignedIn.Should().BeTrue();
        result.Value.Email.Should().Be("player@example.com");
        result.Value.PlayerName.Should().Be("PlayerOne");
        result.Value.SessionKey.Should().Be("sk");

        var status = await auth.GetStatusAsync();
        status.Value!.IsSignedIn.Should().BeTrue();
        status.Value.PlayerUid.Should().Be("u1");
    }

    [Fact]
    public async Task LoginAsync_Fails_WhenPasswordInvalid()
    {
        using var temp = new TempAppPaths();
        var secrets = new FileSecretStore(new FixedPathProvider(temp.Paths));
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("latestunstable", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("1.22.0") };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"valid":0,"reason":"invalidemailorpassword"}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var auth = new AccountAuthService(secrets, NullLogger<AccountAuthService>.Instance, handler);

        var result = await auth.LoginAsync(new AccountCredentials
        {
            Email = "player@example.com",
            Password = "bad",
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("password");
    }

    [Fact]
    public async Task LoginAsync_SignalsTotp_WhenRequired()
    {
        using var temp = new TempAppPaths();
        var secrets = new FileSecretStore(new FixedPathProvider(temp.Paths));
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("latestunstable", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("1.22.0") };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"valid":0,"reason":"requiretotpcode","prelogintoken":"pre-1"}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var auth = new AccountAuthService(secrets, NullLogger<AccountAuthService>.Instance, handler);

        var result = await auth.LoginAsync(new AccountCredentials
        {
            Email = "player@example.com",
            Password = "secret",
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSignedIn.Should().BeFalse();
        result.Value.RequiresTotp.Should().BeTrue();
        result.Value.PreLoginToken.Should().Be("pre-1");
    }
}
