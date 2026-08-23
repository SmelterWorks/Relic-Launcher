using Avalonia.Threading;

namespace RelicLauncher.App.Views.Pages;

public partial class WikiPage
{
    private void NudgeWebViewLayout()
    {
        if (_webView is null)
        {
            return;
        }

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
