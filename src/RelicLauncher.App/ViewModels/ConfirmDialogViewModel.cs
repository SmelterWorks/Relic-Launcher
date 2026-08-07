using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RelicLauncher.App.ViewModels;

public partial class ConfirmDialogViewModel : ObservableObject
{
    public ConfirmDialogViewModel(string title, string message, string confirmText, string cancelText)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        IsDestructive = IsDestructiveConfirmText(confirmText);
    }

    public bool IsDestructive { get; }

    public bool ConfirmIsDefault => !IsDestructive;

    public string Title { get; }

    public string Message { get; }

    public string ConfirmText { get; }

    public string CancelText { get; }

    public bool Confirmed { get; private set; }

    public event EventHandler? RequestClose;

    [RelayCommand]
    private void Confirm()
    {
        Confirmed = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsDestructiveConfirmText(string confirmText)
    {
        var text = confirmText.ToLowerInvariant();
        return text.Contains("uninstall", StringComparison.Ordinal)
            || text.Contains("delete", StringComparison.Ordinal)
            || text.Contains("reset", StringComparison.Ordinal)
            || text.Contains("restore", StringComparison.Ordinal)
            || text.Contains("clean", StringComparison.Ordinal)
            || text.Contains("exit", StringComparison.Ordinal);
    }
}
