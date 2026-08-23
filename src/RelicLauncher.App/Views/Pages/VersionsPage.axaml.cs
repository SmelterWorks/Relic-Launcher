namespace RelicLauncher.App.Views.Pages;

public partial class VersionsPage : Avalonia.Controls.UserControl
{
    public VersionsPage()
    {
        InitializeComponent();
    }

    private void OnSearchKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (DataContext is not ViewModels.VersionsViewModel vm)
        {
            return;
        }

        if (e.Key == Avalonia.Input.Key.Enter)
        {
            vm.RefreshCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Avalonia.Input.Key.Escape)
        {
            vm.SearchText = string.Empty;
            e.Handled = true;
        }
    }
}
