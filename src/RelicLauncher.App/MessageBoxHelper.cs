using Avalonia.Controls;

namespace RelicLauncher.App;

internal static class MessageBoxHelper
{
    public static async Task ShowAsync(Window owner, string message, string title)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(20),
            },
        };

        await dialog.ShowDialog(owner).ConfigureAwait(true);
    }
}
