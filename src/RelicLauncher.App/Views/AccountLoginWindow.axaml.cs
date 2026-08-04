using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Constants;

namespace RelicLauncher.App.Views;

public partial class AccountLoginWindow : Window
{
    private readonly string _accountHome;
    private readonly string? _emailHint;
    private bool _closing;

    public AccountLoginWindow()
        : this(null)
    {
    }

    public AccountLoginWindow(string? emailHint, string? accountBaseUrl = null)
    {
        _emailHint = emailHint;
        _accountHome = NormalizeAccountBase(accountBaseUrl);
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            WebView.Source = new Uri(_accountHome);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not start browser view: " + ex.Message;
        }
    }

    private async void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        try
        {
            var url = WebView.Source?.AbsoluteUri ?? e.Request?.AbsoluteUri ?? string.Empty;
            StatusText.Text = string.IsNullOrWhiteSpace(url) ? "Loaded page." : url;

            if (LooksSignedIn(url))
            {
                StatusText.Text = "Signed in detected. Capturing session...";
                await CompleteWithCookiesAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Navigation error: " + ex.Message;
        }
    }

    private async void OnConfirmClick(object? sender, RoutedEventArgs e)
        => await CompleteWithCookiesAsync().ConfigureAwait(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Close(new AccountLoginWindowResult { Canceled = true });
    }

    private async Task CompleteWithCookiesAsync()
    {
        if (_closing)
        {
            return;
        }

        try
        {
            var manager = WebView.TryGetCookieManager();
            if (manager is null)
            {
                StatusText.Text = "Cookie access is not available on this platform WebView.";
                return;
            }

            var cookies = await manager.GetCookiesAsync().ConfigureAwait(true);
            var accountHost = new Uri(_accountHome).Host;
            var filtered = cookies
                .Where(c => CookieMatchesAccount(c.Domain, accountHost))
                .ToList();

            if (filtered.Count == 0)
            {
                StatusText.Text = "No account cookies found yet. Finish sign-in, then click Use this session.";
                return;
            }

            if (!LooksSignedIn(WebView.Source?.AbsoluteUri ?? string.Empty) &&
                filtered.All(c => string.Equals(c.Name, "PHPSESSID", StringComparison.OrdinalIgnoreCase)))
            {
                StatusText.Text = "Still on the login page. Complete captcha and sign-in, then click Use this session.";
                return;
            }

            _closing = true;
            Close(new AccountLoginWindowResult
            {
                Canceled = false,
                Email = _emailHint,
                Cookies = filtered,
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not read cookies: " + ex.Message;
        }
    }

    private bool LooksSignedIn(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (url.Contains("attemptlogin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = new Uri(_accountHome).Host;
        return url.Contains("/downloads", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("clientarea", StringComparison.OrdinalIgnoreCase) ||
               url.Contains(host + "/files", StringComparison.OrdinalIgnoreCase) ||
               (url.StartsWith(_accountHome, StringComparison.OrdinalIgnoreCase) &&
                !url.TrimEnd('/').Equals(_accountHome.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) &&
                !url.Contains("createaccount", StringComparison.OrdinalIgnoreCase) &&
                !url.Contains("requestresetpwd", StringComparison.OrdinalIgnoreCase));
    }

    private static bool CookieMatchesAccount(string? domain, string accountHost)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        var normalized = domain.Trim().TrimStart('.');
        return normalized.Equals(accountHost, StringComparison.OrdinalIgnoreCase) ||
               accountHost.EndsWith("." + normalized, StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("." + accountHost, StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("vintagestory.at", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAccountBase(string? accountBaseUrl)
    {
        var value = string.IsNullOrWhiteSpace(accountBaseUrl)
            ? VintageStoryEndpoints.AccountBaseUrl
            : accountBaseUrl.Trim();
        return value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
    }
}
