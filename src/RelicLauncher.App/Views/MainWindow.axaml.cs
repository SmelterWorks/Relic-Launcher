using Avalonia.Controls;
using RelicLauncher.App.Services;
using RelicLauncher.App.ViewModels;

namespace RelicLauncher.App.Views;

public partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        ApplyCustomCursor();
        Closing += OnClosing;
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
