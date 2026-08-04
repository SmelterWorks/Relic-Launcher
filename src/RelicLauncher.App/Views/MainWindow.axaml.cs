using Avalonia.Controls;
using RelicLauncher.App.Services;

namespace RelicLauncher.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ApplyCustomCursor();
    }

    private void ApplyCustomCursor()
    {
        var cursor = RelicCursors.TryGetPointer();
        if (cursor is not null)
        {
            Cursor = cursor;
        }
    }
}
