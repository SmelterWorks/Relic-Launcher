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
    private Border? _wheelCatcher;
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
        _wheelCatcher = this.FindControl<Border>("WikiWheelCatcher");
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
        _webView.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);

        _webView.NavigationStarted += OnNavigationStarted;
        _webView.NavigationCompleted += OnNavigationCompleted;
        _webView.NewWindowRequested += OnNewWindowRequested;
        _webView.AdapterCreated += OnAdapterCreated;
        _webView.AdapterDestroyed += OnAdapterDestroyed;
        _webView.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        if (_wheelCatcher is not null)
        {
            _wheelCatcher.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
            _wheelCatcher.PointerPressed -= OnWheelCatcherPressed;
            _wheelCatcher.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            _wheelCatcher.PointerPressed += OnWheelCatcherPressed;
        }

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

        if (_wheelCatcher is not null)
        {
            _wheelCatcher.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
            _wheelCatcher.PointerPressed -= OnWheelCatcherPressed;
        }

        if (_webView is not null)
        {
            _webView.NavigationStarted -= OnNavigationStarted;
            _webView.NavigationCompleted -= OnNavigationCompleted;
            _webView.NewWindowRequested -= OnNewWindowRequested;
            _webView.AdapterCreated -= OnAdapterCreated;
            _webView.AdapterDestroyed -= OnAdapterDestroyed;
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
                _ = InjectWheelAssistAsync();
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
        if (_webView is null || _boundVm?.HasError == true || !IsVisible)
        {
            return;
        }

        var point = e.GetPosition(_webView);
        if (point.X < 0 || point.Y < 0 || point.X > _webView.Bounds.Width || point.Y > _webView.Bounds.Height)
        {
            return;
        }

        ForwardWheel(e);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        => ForwardWheel(e);

    private void ForwardWheel(PointerWheelEventArgs e)
    {
        if (_webView is null || _boundVm?.HasError == true)
        {
            return;
        }

        var dx = (-e.Delta.X * 80).ToString(CultureInfo.InvariantCulture);
        var dy = (-e.Delta.Y * 80).ToString(CultureInfo.InvariantCulture);
        _ = ScrollWebViewAsync(dx, dy);
        e.Handled = true;
    }

    private void OnWheelCatcherPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_webView is null)
        {
            return;
        }

        var point = e.GetPosition(_webView);
        var x = point.X.ToString(CultureInfo.InvariantCulture);
        var y = point.Y.ToString(CultureInfo.InvariantCulture);
        _ = ClickWebViewAsync(x, y);
        e.Handled = true;
    }

    private async Task ClickWebViewAsync(string x, string y)
    {
        if (_webView is null)
        {
            return;
        }

        try
        {
            await _webView.InvokeScript(
                    $"(function(x,y){{var el=document.elementFromPoint(x,y);if(!el)return;" +
                    $"['mousedown','mouseup','click'].forEach(function(t){{" +
                    $"el.dispatchEvent(new MouseEvent(t,{{bubbles:true,cancelable:true,view:window,clientX:x,clientY:y}}));}}" +
                    $");if(el.focus)el.focus();}})({x},{y});")
                .ConfigureAwait(true);
        }
        catch
        {
            // Ignore script failures while the page is still loading.
        }
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
                    $"function canScroll(el){{if(!el)return false;var s=getComputedStyle(el);var oy=s.overflowY;var ox=s.overflowX;" +
                    $"var y=(oy==='auto'||oy==='scroll'||oy==='overlay')&&el.scrollHeight>el.clientHeight+1;" +
                    $"var x=(ox==='auto'||ox==='scroll'||ox==='overlay')&&el.scrollWidth>el.clientWidth+1;return y||x;}}" +
                    $"var nodes=[document.getElementById('content'),document.getElementById('bodyContent')," +
                    $"document.getElementById('mw-content-text'),document.querySelector('.mw-body')," +
                    $"document.querySelector('.vector-body'),document.scrollingElement,document.documentElement,document.body];" +
                    $"for(var i=0;i<nodes.length;i++){{var n=nodes[i];if(!n)continue;var beforeY=n.scrollTop,beforeX=n.scrollLeft;" +
                    $"n.scrollBy(dx,dy);if(n.scrollTop!==beforeY||n.scrollLeft!==beforeX)return;}}" +
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

    private async Task InjectWheelAssistAsync()
    {
        if (_webView is null)
        {
            return;
        }

        try
        {
            await _webView.InvokeScript(
                    "(function(){if(window.__relicWheelAssist)return;window.__relicWheelAssist=true;" +
                    "document.addEventListener('wheel',function(e){" +
                    "var root=document.scrollingElement||document.documentElement||document.body;" +
                    "if(!root)return;" +
                    "var before=root.scrollTop;" +
                    "root.scrollTop+=e.deltaY;" +
                    "root.scrollLeft+=e.deltaX;" +
                    "if(root.scrollTop===before){" +
                    "var c=document.getElementById('content')||document.getElementById('bodyContent')||document.getElementById('mw-content-text');" +
                    "if(c){c.scrollTop+=e.deltaY;c.scrollLeft+=e.deltaX;}" +
                    "}" +
                    "},{passive:true});})();")
                .ConfigureAwait(true);
        }
        catch
        {
            // Best effort.
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
