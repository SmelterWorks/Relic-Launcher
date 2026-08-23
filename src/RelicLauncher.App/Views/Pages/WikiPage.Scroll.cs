using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RelicLauncher.App.Services;
using RelicLauncher.App.ViewModels;

namespace RelicLauncher.App.Views.Pages;

public partial class WikiPage
{
    private void RegisterScrollWheelCapture()
    {
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.RemoveHandler(InputElement.PointerWheelChangedEvent, OnTopLevelWheelChanged);
        _topLevel.AddHandler(
            InputElement.PointerWheelChangedEvent,
            OnTopLevelWheelChanged,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private void UnregisterScrollWheelCapture()
    {
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.RemoveHandler(InputElement.PointerWheelChangedEvent, OnTopLevelWheelChanged);
        _topLevel = null;
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
            await _webView.InvokeScript(WikiWebViewScrollScript.Build(dx, dy)).ConfigureAwait(true);
        }
        catch
        {
            // Ignore script failures while the page is still loading.
        }
    }
}
