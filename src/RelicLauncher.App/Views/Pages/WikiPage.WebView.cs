using Avalonia.Controls;
using RelicLauncher.App.Services;
using RelicLauncher.App.ViewModels;

namespace RelicLauncher.App.Views.Pages;

public partial class WikiPage
{
    private void TryEnsureWebView()
    {
        if (_webView is not null || _webViewHost is null || _boundVm is null)
        {
            return;
        }

        if (!_boundVm.ShowWebView || !_boundVm.IsWebViewAvailable)
        {
            return;
        }

        if (OperatingSystem.IsLinux() && !LinuxEmbeddedBrowserSupport.IsLikelyAvailable())
        {
            _boundVm.ReportWebViewUnavailable(
                "Embedded browser libraries are not available in this environment. Open the wiki in your browser.");
            return;
        }

        try
        {
            _webView = new NativeWebView
            {
                Focusable = true,
            };
            _webView.NavigationStarted += OnNavigationStarted;
            _webView.NavigationCompleted += OnNavigationCompleted;
            _webView.NewWindowRequested += OnNewWindowRequested;
            _webView.AdapterCreated += OnAdapterCreated;
            _webView.AdapterDestroyed += OnAdapterDestroyed;
            _webViewHost.Children.Add(_webView);
            NudgeWebViewLayout();
        }
        catch (DllNotFoundException ex)
        {
            _boundVm.ReportWebViewUnavailable(ex.Message);
            DestroyWebView();
        }
        catch (Exception)
        {
            _boundVm.ReportWebViewUnavailable();
            DestroyWebView();
        }
    }

    private void DestroyWebView()
    {
        if (_webView is null)
        {
            return;
        }

        _webView.NavigationStarted -= OnNavigationStarted;
        _webView.NavigationCompleted -= OnNavigationCompleted;
        _webView.NewWindowRequested -= OnNewWindowRequested;
        _webView.AdapterCreated -= OnAdapterCreated;
        _webView.AdapterDestroyed -= OnAdapterDestroyed;
        _webViewHost?.Children.Remove(_webView);
        _webView = null;
    }

    private void NavigateTo(Uri uri)
    {
        TryEnsureWebView();
        if (_webView is null)
        {
            _boundVm?.ReportWebViewUnavailable();
            return;
        }

        try
        {
            _suppressNavigationHandler = false;
            _webView.Navigate(uri);
            NudgeWebViewLayout();
        }
        catch (Exception)
        {
            _boundVm?.ReportWebViewUnavailable();
        }
    }

    private void Reload()
    {
        try
        {
            _webView?.Refresh();
        }
        catch (Exception)
        {
            _boundVm?.ReportWebViewUnavailable();
        }
    }

    private bool GoBack()
    {
        try
        {
            return _webView?.GoBack() == true;
        }
        catch (Exception)
        {
            _boundVm?.ReportWebViewUnavailable();
            return false;
        }
    }

    private bool GoForward()
    {
        try
        {
            return _webView?.GoForward() == true;
        }
        catch (Exception)
        {
            _boundVm?.ReportWebViewUnavailable();
            return false;
        }
    }

    private void ClearSiteData()
    {
        if (_webView is null)
        {
            return;
        }

        try
        {
            _suppressNavigationHandler = true;
            var cookies = _webView.TryGetCookieManager();
            if (cookies is not null)
            {
                _ = ClearCookiesAsync(cookies);
            }

            _webView.Navigate(new Uri("about:blank"));
        }
        catch
        {
            // Best effort when switching wiki hosts.
        }
        finally
        {
            _suppressNavigationHandler = false;
        }
    }

    private static async Task ClearCookiesAsync(NativeWebViewCookieManager cookies)
    {
        try
        {
            var list = await cookies.GetCookiesAsync().ConfigureAwait(true);
            foreach (var cookie in list)
            {
                cookies.DeleteCookie(cookie);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}
