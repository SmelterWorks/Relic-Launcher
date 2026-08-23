using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using RelicLauncher.App.Services;
using RelicLauncher.App.ViewModels;

namespace RelicLauncher.App.Views;

public partial class MainWindow : Window
{
    public const double CustomChromeHeight = 36;

    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        WireResizeGrips();
        ApplyCustomCursor();
        Closing += OnClosing;
        Opened += OnOpened;
    }

    private void WireResizeGrips()
    {
        WireResizeGrip(NorthWestResizeGrip, WindowEdge.NorthWest);
        WireResizeGrip(NorthEastResizeGrip, WindowEdge.NorthEast);
        WireResizeGrip(SouthWestResizeGrip, WindowEdge.SouthWest);
        WireResizeGrip(SouthEastResizeGrip, WindowEdge.SouthEast);
        WireResizeGrip(NorthResizeGrip, WindowEdge.North);
        WireResizeGrip(SouthResizeGrip, WindowEdge.South);
        WireResizeGrip(WestResizeGrip, WindowEdge.West);
        WireResizeGrip(EastResizeGrip, WindowEdge.East);
    }

    private void WireResizeGrip(Panel grip, WindowEdge edge)
    {
        grip.PointerPressed += (_, e) => OnResizeGripPointerPressed(edge, e);
    }

    private void OnResizeGripPointerPressed(WindowEdge edge, PointerPressedEventArgs e)
    {
        if (!CanResize)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        BeginResizeDrag(edge, e);
        e.Handled = true;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            ApplyWindowChrome(vm.Settings.UseNativeTitleBar);
        }
    }

    public void ApplyWindowChrome(bool useNativeTitleBar)
    {
        WindowDecorations = useNativeTitleBar ? WindowDecorations.Full : WindowDecorations.None;
        ExtendClientAreaToDecorationsHint = !useNativeTitleBar;
        ExtendClientAreaTitleBarHeightHint = useNativeTitleBar ? 0 : CustomChromeHeight;

        CustomChrome.IsVisible = !useNativeTitleBar;

        var showResizeGrips = !useNativeTitleBar && CanResize;
        NorthWestResizeGrip.IsVisible = showResizeGrips;
        NorthEastResizeGrip.IsVisible = showResizeGrips;
        SouthWestResizeGrip.IsVisible = showResizeGrips;
        SouthEastResizeGrip.IsVisible = showResizeGrips;
        NorthResizeGrip.IsVisible = showResizeGrips;
        SouthResizeGrip.IsVisible = showResizeGrips;
        WestResizeGrip.IsVisible = showResizeGrips;
        EastResizeGrip.IsVisible = showResizeGrips;

        if (ShellGrid.RowDefinitions.Count > 0)
        {
            ShellGrid.RowDefinitions[0].Height = useNativeTitleBar
                ? new GridLength(0)
                : new GridLength(CustomChromeHeight);
        }
    }

    private void ApplyCustomCursor()
    {
        var cursor = RelicCursors.TryGetPointer();
        if (cursor is not null)
        {
            Cursor = cursor;
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose || DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        e.Cancel = true;
        if (await vm.ConfirmCloseAsync().ConfigureAwait(true))
        {
            _allowClose = true;
            Close();
        }
    }
}
