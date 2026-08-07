using Avalonia.Controls;
using RelicLauncher.App.ViewModels;

namespace RelicLauncher.App.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    public static async Task<bool> ShowAsync(
        Window owner,
        string title,
        string message,
        string confirmText,
        string cancelText)
    {
        var vm = new ConfirmDialogViewModel(title, message, confirmText, cancelText);
        var window = new ConfirmDialog { DataContext = vm };
        vm.RequestClose += (_, _) => window.Close();
        await window.ShowDialog(owner).ConfigureAwait(true);
        return vm.Confirmed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not ConfirmDialogViewModel vm)
        {
            return;
        }

        if (vm.IsDestructive)
        {
            CancelButton?.Focus();
        }
        else
        {
            ConfirmButton?.Focus();
        }
    }
}
