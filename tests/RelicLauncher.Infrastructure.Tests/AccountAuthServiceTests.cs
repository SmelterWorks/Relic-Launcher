using System.Net;
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
    public async Task LoginAsync_PersistsSession_WhenRedirectedAwayFromLogin()
    {
        using var temp = new TempAppPaths();
        var secrets = new FileSecretStore(new FixedPathProvider(temp.Paths));
        var cookies = new CookieContainer();
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://account.vintagestory.at/downloads");
            response.Headers.Add("Set-Cookie", "vsid=test-session; Path=/; HttpOnly");
            return response;
        });
        var auth = new AccountAuthService(secrets, NullLogger<AccountAuthService>.Instance, handler, cookies);

        var result = await auth.LoginAsync(new AccountCredentials
        {
            Email = "player@example.com",
            Password = "secret",
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSignedIn.Should().BeTrue();
        result.Value.Email.Should().Be("player@example.com");

        var status = await auth.GetStatusAsync();
        status.Value!.IsSignedIn.Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_Fails_WhenLoginFormReturned()
    {
        using var temp = new TempAppPaths();
        var secrets = new FileSecretStore(new FixedPathProvider(temp.Paths));
        var cookies = new CookieContainer();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<form method=\"post\" action=\"attemptlogin\"><input type=\"password\"></form>"),
        });
        var auth = new AccountAuthService(secrets, NullLogger<AccountAuthService>.Instance, handler, cookies);

        var result = await auth.LoginAsync(new AccountCredentials
        {
            Email = "player@example.com",
            Password = "bad",
        });

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ImportBrowserSessionAsync_PersistsCookiesAndEmail()
    {
        using var temp = new TempAppPaths();
        var secrets = new FileSecretStore(new FixedPathProvider(temp.Paths));
        var cookies = new CookieContainer();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var auth = new AccountAuthService(secrets, NullLogger<AccountAuthService>.Instance, handler, cookies);

        var result = await auth.ImportBrowserSessionAsync(
            "player@example.com",
            [
                new Cookie("PHPSESSID", "abc", "/", ".vintagestory.at") { Secure = true, HttpOnly = true },
                new Cookie("vsid", "session", "/", ".vintagestory.at") { Secure = true, HttpOnly = true },
            ]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSignedIn.Should().BeTrue();
        (await auth.GetStatusAsync()).Value!.Email.Should().Be("player@example.com");
    }
}
