using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class SettingsViewModel
{
    [RelayCommand]
    private async Task SignInAsync()
    {
        IsSigningIn = true;
        StatusMessage = string.Empty;
        StatusIsError = false;
        AccountError = string.Empty;
        AccountStatus = RequiresTotp ? "Checking access code..." : "Signing in...";
        try
        {
            var result = await _accountAuth.LoginAsync(new AccountCredentials
            {
                Email = AccountEmail,
                Password = AccountPassword,
                TotpCode = AccountTotpCode,
                PreLoginToken = _preLoginToken,
            }).ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                var error = result.Error ?? "Sign-in failed.";
                AccountError = error;
                SetStatus(error, true);
                IsSignedIn = false;
                AccountStatus = "Not signed in";
                _logger.LogWarning("Settings game sign-in failed: {Error}", error);
                return;
            }

            if (result.Value!.RequiresTotp)
            {
                RequiresTotp = true;
                _preLoginToken = result.Value.PreLoginToken;
                IsSignedIn = false;
                AccountStatus = "Enter your 6-digit access code.";
                AccountError = string.IsNullOrWhiteSpace(AccountTotpCode)
                    ? string.Empty
                    : "Wrong access code. Try again.";
                SetStatus(AccountStatus);
                return;
            }

            RequiresTotp = false;
            _preLoginToken = null;
            AccountTotpCode = string.Empty;
            IsSignedIn = true;
            AccountEmail = result.Value.Email ?? AccountEmail;
            AccountPassword = string.Empty;
            AccountError = string.Empty;
            AccountStatus = string.IsNullOrWhiteSpace(result.Value.PlayerName)
                ? $"Signed in as {result.Value.Email}"
                : $"Signed in as {result.Value.PlayerName}";
            SetStatus("Signed in. Relic will pass this session to the game on Play.");
            _logger.LogInformation("Settings game sign-in succeeded for {Email}", AccountEmail);
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _accountAuth.LogoutAsync().ConfigureAwait(true);
        var dataPath = string.IsNullOrWhiteSpace(DataPath)
            ? _platform.GetPlatformInfo().DefaultDataPath
            : DataPath.Trim();
        await _sessionWriter.ClearSessionAsync(dataPath).ConfigureAwait(true);
        IsSignedIn = false;
        RequiresTotp = false;
        _preLoginToken = null;
        AccountTotpCode = string.Empty;
        AccountPassword = string.Empty;
        AccountStatus = "Not signed in";
        AccountError = string.Empty;
        SetStatus("Signed out.");
    }
    private async Task RefreshAccountStatusAsync()
    {
        var status = await _accountAuth.GetStatusAsync().ConfigureAwait(true);
        if (!status.IsSuccess || status.Value is null || !status.Value.IsSignedIn)
        {
            IsSignedIn = false;
            AccountStatus = "Not signed in";
            return;
        }

        IsSignedIn = true;
        AccountEmail = status.Value.Email ?? AccountEmail;
        AccountStatus = string.IsNullOrWhiteSpace(status.Value.PlayerName)
            ? $"Signed in as {status.Value.Email}"
            : $"Signed in as {status.Value.PlayerName}";
    }
}
