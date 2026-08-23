using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace RelicLauncher.App.Views.Controls;

public partial class WindowChrome : UserControl
{
    private Window? _window;

    public WindowChrome()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is null)
        {
            return;
        }

        _window.PropertyChanged += OnWindowPropertyChanged;
        UpdateMaximizeChrome();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window = null;
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            UpdateMaximizeChrome();
        }
    }

    private void UpdateMaximizeChrome()
    {
        var maximized = _window?.WindowState == WindowState.Maximized;
        MaximizeIcon.Value = maximized ? "mdi-window-restore" : "mdi-window-maximize";
        ToolTip.SetTip(MaximizeButton, maximized ? "Restore" : "Maximize");
        AutomationProperties.SetName(MaximizeButton, maximized ? "Restore window" : "Maximize window");
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        if (_window is null)
        {
            return;
        }

        _window.WindowState = WindowState.Minimized;
        e.Handled = true;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        if (_window is null || !_window.CanResize)
        {
            return;
        }

        _window.WindowState = _window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        e.Handled = true;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (_window is null)
        {
            return;
        }

        _window.Close();
        e.Handled = true;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_window is null)
        {
            return;
        }

        if (e.Source is Visual source && source.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        var point = e.GetCurrentPoint(_window);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.ClickCount == 2 && _window.CanResize)
        {
            _window.WindowState = _window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        _window.BeginMoveDrag(e);
        e.Handled = true;
    }
}
