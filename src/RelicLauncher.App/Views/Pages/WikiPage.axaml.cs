using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RelicLauncher.App.ViewModels;
using RelicLauncher.Core.Wiki;

namespace RelicLauncher.App.Views.Pages;

public partial class WikiPage : UserControl
{
    private NativeWebView? _webView;
    private WikiViewModel? _boundVm;
    private TopLevel? _topLevel;
    private bool _suppressNavigationHandler;

    public WikiPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
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

        _webView.NavigationStarted += OnNavigationStarted;
        _webView.NavigationCompleted += OnNavigationCompleted;
        _webView.NewWindowRequested += OnNewWindowRequested;
        _webView.AdapterCreated += OnAdapterCreated;
        _webView.AdapterDestroyed += OnAdapterDestroyed;

        // The embedded WebKitGTK fallback (used when WPE is not installed) does not
        // reliably forward wheel input to the browser engine, so intercept it at the
        // TopLevel and drive scrolling through script instead. handledEventsToo is
        // required because the WebView's own input handling may mark the event
        // handled before it reaches us.
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.RemoveHandler(InputElement.PointerWheelChangedEvent, OnTopLevelWheelChanged);
            _topLevel.AddHandler(
                InputElement.PointerWheelChangedEvent,
                OnTopLevelWheelChanged,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
        }

        HookViewModel(DataContext as WikiViewModel);
        NudgeWebViewLayout();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null)
        {
            _topLevel.RemoveHandler(InputElement.PointerWheelChangedEvent, OnTopLevelWheelChanged);
            _topLevel = null;
        }

        if (_webView is not null)
        {
            _webView.NavigationStarted -= OnNavigationStarted;
            _webView.NavigationCompleted -= OnNavigationCompleted;
            _webView.NewWindowRequested -= OnNewWindowRequested;
            _webView.AdapterCreated -= OnAdapterCreated;
            _webView.AdapterDestroyed -= OnAdapterDestroyed;
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

    private void OnTopLevelWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_webView is null || _boundVm?.HasError == true || !IsEffectivelyVisible)
        {
            return;
        }

        var point = e.GetPosition(_webView);
        if (point.X < 0 || point.Y < 0 || point.X > _webView.Bounds.Width || point.Y > _webView.Bounds.Height)
        {
            return;
        }

        var dx = (-e.Delta.X * 100).ToString(CultureInfo.InvariantCulture);
        var dy = (-e.Delta.Y * 100).ToString(CultureInfo.InvariantCulture);
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
                    $"(function(dx,dy){{" +
                    $"var root=document.scrollingElement||document.documentElement||document.body;" +
                    $"if(root){{var beforeY=root.scrollTop,beforeX=root.scrollLeft;root.scrollBy(dx,dy);" +
                    $"if(root.scrollTop!==beforeY||root.scrollLeft!==beforeX)return;}}" +
                    $"function canScroll(el){{if(!el)return false;var s=getComputedStyle(el);" +
                    $"var y=(s.overflowY==='auto'||s.overflowY==='scroll')&&el.scrollHeight>el.clientHeight+1;" +
                    $"var x=(s.overflowX==='auto'||s.overflowX==='scroll')&&el.scrollWidth>el.clientWidth+1;return y||x;}}" +
                    $"var el=document.elementFromPoint(Math.floor(window.innerWidth/2),Math.floor(window.innerHeight/2));" +
                    $"while(el){{if(canScroll(el)){{el.scrollBy(dx,dy);return;}}el=el.parentElement;}}" +
                    $"window.scrollBy(dx,dy);" +
                    $"}})({dx},{dy});")
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
