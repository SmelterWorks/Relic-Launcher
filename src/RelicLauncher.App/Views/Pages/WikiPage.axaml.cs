using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using RelicLauncher.App.ViewModels;
using RelicLauncher.Core.Wiki;

namespace RelicLauncher.App.Views.Pages;

public partial class WikiPage : UserControl
{
    private NativeWebView? _webView;
    private WikiViewModel? _boundVm;
    private bool _suppressNavigationHandler;

    public WikiPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _webView = this.FindControl<NativeWebView>("WikiWebView");
        if (_webView is null)
        {
            return;
        }

        _webView.Focusable = true;
        _webView.NavigationStarted -= OnNavigationStarted;
        _webView.NavigationCompleted -= OnNavigationCompleted;
        _webView.NewWindowRequested -= OnNewWindowRequested;
        _webView.AdapterCreated -= OnAdapterCreated;
        _webView.AdapterDestroyed -= OnAdapterDestroyed;
        _webView.EnvironmentRequested -= OnEnvironmentRequested;
        _webView.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);

        _webView.NavigationStarted += OnNavigationStarted;
        _webView.NavigationCompleted += OnNavigationCompleted;
        _webView.NewWindowRequested += OnNewWindowRequested;
        _webView.AdapterCreated += OnAdapterCreated;
        _webView.AdapterDestroyed += OnAdapterDestroyed;
        _webView.EnvironmentRequested += OnEnvironmentRequested;
        _webView.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);

        HookViewModel(DataContext as WikiViewModel);
        NudgeWebViewLayout();
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_webView is not null)
        {
            _webView.NavigationStarted -= OnNavigationStarted;
            _webView.NavigationCompleted -= OnNavigationCompleted;
            _webView.NewWindowRequested -= OnNewWindowRequested;
            _webView.AdapterCreated -= OnAdapterCreated;
            _webView.AdapterDestroyed -= OnAdapterDestroyed;
            _webView.EnvironmentRequested -= OnEnvironmentRequested;
            _webView.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
        }

        UnhookViewModel();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
        => HookViewModel(DataContext as WikiViewModel);

    private void HookViewModel(WikiViewModel? vm)
    {
        if (ReferenceEquals(_boundVm, vm))
        {
            return;
        }

        UnhookViewModel();
        _boundVm = vm;
        if (vm is null)
        {
            return;
        }

        vm.NavigateRequested = NavigateTo;
        vm.ReloadRequested = Reload;
        vm.GoBackRequested = GoBack;
        vm.GoForwardRequested = GoForward;
        vm.ClearSiteDataRequested = ClearSiteData;
        vm.NotifyHostReady();
    }

    private void UnhookViewModel()
    {
        if (_boundVm is null)
        {
            return;
        }

        _boundVm.NavigateRequested = null;
        _boundVm.ReloadRequested = null;
        _boundVm.GoBackRequested = null;
        _boundVm.GoForwardRequested = null;
        _boundVm.ClearSiteDataRequested = null;
        _boundVm = null;
    }

    private void NavigateTo(Uri uri)
    {
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
                cookies.DeleteCookie(cookie.Name, cookie.Path, cookie.Domain);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        if (_suppressNavigationHandler || _boundVm is null)
        {
            return;
        }

        var url = e.Request?.AbsoluteUri;
        if (string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            return;
        }

        var decision = _boundVm.EvaluateNavigation(url, out var resolved);
        switch (decision)
        {
            case WikiNavigationDecision.Allow when resolved is not null:
                _boundVm.HandleAllowedNavigationStarted(resolved);
                break;
            case WikiNavigationDecision.OpenExternally when resolved is not null:
                e.Cancel = true;
                _boundVm.HandleExternalNavigation(resolved);
                break;
            default:
                e.Cancel = true;
                break;
        }
    }

    private void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (_suppressNavigationHandler || _boundVm is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_webView is not null)
            {
                _boundVm.UpdateHistoryState(_webView.CanGoBack, _webView.CanGoForward);
                NudgeWebViewLayout();
            }

            _boundVm.HandleNavigationCompleted(e.Request, e.IsSuccess);
        });
    }

    private void OnNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (_boundVm is null)
        {
            return;
        }

        var decision = _boundVm.EvaluateNavigation(e.Request?.AbsoluteUri, out var resolved);
        if (decision == WikiNavigationDecision.Allow && resolved is not null)
        {
            NavigateTo(resolved);
            return;
        }

        if (decision == WikiNavigationDecision.OpenExternally && resolved is not null)
        {
            _boundVm.HandleExternalNavigation(resolved);
        }
    }

    private void OnAdapterCreated(object? sender, WebViewAdapterEventArgs e)
    {
        _ = e;
        NudgeWebViewLayout();
    }

    private void OnAdapterDestroyed(object? sender, WebViewAdapterEventArgs e)
    {
        _ = e;
        Dispatcher.UIThread.Post(() => _boundVm?.ReportWebViewUnavailable());
    }

    private void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
    {
        _ = sender;
        if (e is LinuxWpeWebViewEnvironmentRequestedEventArgs linux)
        {
            // Improves wheel/input routing on some Linux sessions.
            linux.PreferWebKitGtkInstead = true;
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_webView is null || _boundVm?.HasError == true)
        {
            return;
        }

        var dx = (-e.Delta.X * 64).ToString(CultureInfo.InvariantCulture);
        var dy = (-e.Delta.Y * 64).ToString(CultureInfo.InvariantCulture);
        _ = ScrollWebViewAsync(dx, dy);
        e.Handled = true;
    }

    private async Task ScrollWebViewAsync(string dx, string dy)
    {
        if (_webView is null)
        {
            return;
        }

        try
        {
            await _webView.InvokeScript(
                $"(function(){{var t=document.scrollingElement||document.documentElement||document.body; if(t){{t.scrollBy({dx},{dy});}} else {{window.scrollBy({dx},{dy});}} }})();")
                .ConfigureAwait(true);
        }
        catch
        {
            // Ignore script failures while the page is still loading.
        }
    }

    private void NudgeWebViewLayout()
    {
        if (_webView is null)
        {
            return;
        }

        // NativeWebView can attach with a 1x1 host slot until measure is nudged.
        void Invalidate()
        {
            if (_webView is null)
            {
                return;
            }

            _webView.InvalidateMeasure();
            _webView.InvalidateArrange();
            _webView.InvalidateVisual();
        }

        Dispatcher.UIThread.Post(Invalidate, DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(Invalidate, DispatcherPriority.Background);
    }
}
