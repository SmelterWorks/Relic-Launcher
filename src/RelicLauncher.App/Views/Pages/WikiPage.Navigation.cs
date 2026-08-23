using Avalonia.Controls;
using Avalonia.Threading;
using RelicLauncher.App.ViewModels;
using RelicLauncher.Core.Wiki;

namespace RelicLauncher.App.Views.Pages;

public partial class WikiPage
{
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
}
