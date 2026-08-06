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
        var secrets = new PlatformSecretStore(new FixedPathProvider(temp.Paths));
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
        var secrets = new PlatformSecretStore(new FixedPathProvider(temp.Paths));
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
        var secrets = new PlatformSecretStore(new FixedPathProvider(temp.Paths));
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

    [Fact]
    public async Task ValidateSessionAsync_Fails_WhenNotSignedIn()
    {
        using var temp = new TempAppPaths();
        var secrets = new PlatformSecretStore(new FixedPathProvider(temp.Paths));
        var handler = new StubHandler(_ => throw new InvalidOperationException("No request expected when signed out."));
        var auth = new AccountAuthService(secrets, NullLogger<AccountAuthService>.Instance, handler);

        var result = await auth.ValidateSessionAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Sign in");
    }

    [Fact]
    public async Task ValidateSessionAsync_Fails_WhenLocalSignatureInvalid()
    {
        using var temp = new TempAppPaths();
        var secrets = new PlatformSecretStore(new FixedPathProvider(temp.Paths));
        var auth = await LoginWithFakeSessionAsync(secrets);

        var result = await auth.ValidateSessionAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("invalid");

        var status = await auth.GetStatusAsync();
        status.Value!.IsSignedIn.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSessionAsync_Succeeds_WhenServerAcceptsSession()
    {
        using var temp = new TempAppPaths();
        var secrets = new PlatformSecretStore(new FixedPathProvider(temp.Paths));
        var validateHandler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("clientvalidate", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"valid":1,"entitlements":"e2","hasgameserver":true}""", Encoding.UTF8, "application/json"),
                };
            }

            throw new InvalidOperationException("Unexpected request: " + request.RequestUri);
        });
        var auth = await LoginWithFakeSessionAsync(secrets, new AlwaysValidSessionSignatureValidator(), validateHandler);

        var result = await auth.ValidateSessionAsync();

        result.IsSuccess.Should().BeTrue();
        var status = await auth.GetStatusAsync();
        status.Value!.IsSignedIn.Should().BeTrue();
        status.Value.Entitlements.Should().Be("e2");
    }

    [Fact]
    public async Task ValidateSessionAsync_ClearsSession_WhenServerRejects()
    {
        using var temp = new TempAppPaths();
        var secrets = new PlatformSecretStore(new FixedPathProvider(temp.Paths));
        var validateHandler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("clientvalidate", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"valid":0,"reason":"invalidsession"}""", Encoding.UTF8, "application/json"),
                };
            }

            throw new InvalidOperationException("Unexpected request: " + request.RequestUri);
        });
        var auth = await LoginWithFakeSessionAsync(secrets, new AlwaysValidSessionSignatureValidator(), validateHandler);

        var result = await auth.ValidateSessionAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("expired");
        var status = await auth.GetStatusAsync();
        status.Value!.IsSignedIn.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSessionAsync_TreatsNetworkFailure_AsOffline()
    {
        using var temp = new TempAppPaths();
        var secrets = new PlatformSecretStore(new FixedPathProvider(temp.Paths));
        var validateHandler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("clientvalidate", StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException("offline");
            }

            throw new InvalidOperationException("Unexpected request: " + request.RequestUri);
        });
        var auth = await LoginWithFakeSessionAsync(secrets, new AlwaysValidSessionSignatureValidator(), validateHandler);

        var result = await auth.ValidateSessionAsync();

        result.IsSuccess.Should().BeTrue();
        var status = await auth.GetStatusAsync();
        status.Value!.IsSignedIn.Should().BeTrue();
    }

    private static async Task<AccountAuthService> LoginWithFakeSessionAsync(
        PlatformSecretStore secrets,
        ISessionSignatureValidator? sessionSignatureValidator = null,
        StubHandler? followUpHandler = null)
    {
        var loginHandler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("latestunstable", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("1.22.0") };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"valid":1,"sessionkey":"sk","sessionsignature":"sig","uid":"u1","playername":"PlayerOne","entitlements":"e1","mptoken":"mp","hasgameserver":false}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var loginAuth = new AccountAuthService(secrets, NullLogger<AccountAuthService>.Instance, loginHandler, sessionSignatureValidator);
        var login = await loginAuth.LoginAsync(new AccountCredentials { Email = "player@example.com", Password = "secret" }).ConfigureAwait(false);
        login.IsSuccess.Should().BeTrue();

        if (followUpHandler is null)
        {
            return loginAuth;
        }

        return new AccountAuthService(secrets, NullLogger<AccountAuthService>.Instance, followUpHandler, sessionSignatureValidator);
    }

    private sealed class AlwaysValidSessionSignatureValidator : ISessionSignatureValidator
    {
        public bool IsValid(string? sessionKey, string? sessionSignature, string? playerUid) => true;
    }
}
