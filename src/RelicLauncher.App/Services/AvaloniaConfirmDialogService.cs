using Avalonia.Controls;
using RelicLauncher.App.Views;

namespace RelicLauncher.App.Services;

public sealed class AvaloniaConfirmDialogService : IConfirmDialogService
{
    private readonly MainWindowHolder _windowHolder;

    public AvaloniaConfirmDialogService(MainWindowHolder windowHolder)
    {
        _windowHolder = windowHolder;
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Confirm",
        string cancelText = "Cancel")
    {
        var owner = _windowHolder.Window;
        if (owner is null)
        {
            return false;
        }

        return await ConfirmDialog.ShowAsync(owner, title, message, confirmText, cancelText)
            .ConfigureAwait(true);
    }
}
