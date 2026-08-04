using System.Net;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.App.Services;

public sealed class AccountBrowserLoginService : IAccountBrowserLoginService
{
    private readonly MainWindowHolder _windowHolder;
    private readonly IAccountAuthService _accountAuth;
    private readonly IEndpointProvider _endpoints;
    private readonly ILogger<AccountBrowserLoginService> _logger;

    public AccountBrowserLoginService(
        MainWindowHolder windowHolder,
        IAccountAuthService accountAuth,
        IEndpointProvider endpoints,
        ILogger<AccountBrowserLoginService> logger)
    {
        _windowHolder = windowHolder;
        _accountAuth = accountAuth;
        _endpoints = endpoints;
        _logger = logger;
    }

    public async Task<Result<AccountSessionStatus>> SignInAsync(string? emailHint, CancellationToken cancellationToken = default)
    {
        var owner = _windowHolder.Window;
        if (owner is null)
        {
            return Result<AccountSessionStatus>.Failure("Main window is not ready for browser sign-in.");
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            return await Dispatcher.UIThread.InvokeAsync(() => SignInAsync(emailHint, cancellationToken)).ConfigureAwait(false);
        }

        var dialog = new Views.AccountLoginWindow(emailHint, _endpoints.AccountBaseUrl);
        try
        {
            var outcome = await dialog.ShowDialog<AccountLoginWindowResult?>(owner).ConfigureAwait(true);
            if (outcome is null || outcome.Canceled)
            {
                return Result<AccountSessionStatus>.Failure("Sign-in was canceled.");
            }

            if (outcome.Cookies.Count == 0)
            {
                return Result<AccountSessionStatus>.Failure("No account cookies were captured from the browser session.");
            }

            var email = string.IsNullOrWhiteSpace(outcome.Email) ? emailHint?.Trim() : outcome.Email.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                email = "game-account";
            }

            return await _accountAuth.ImportBrowserSessionAsync(email, outcome.Cookies, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Browser sign-in failed");
            return Result<AccountSessionStatus>.Failure(
                "Browser sign-in failed. On Linux install WebKit/WPE packages for Avalonia WebView, or try again. " + ex.Message);
        }
    }
}
