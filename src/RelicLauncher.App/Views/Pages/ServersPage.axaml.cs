using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace RelicLauncher.App.Views.Pages;

public partial class ServersPage : UserControl
{
    public ServersPage()
    {
        InitializeComponent();
    }

    private async void OnCopyAddressClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.ServersViewModel vm ||
            string.IsNullOrWhiteSpace(vm.SelectedDetailAddress))
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            return;
        }

        await topLevel.Clipboard.SetTextAsync(vm.SelectedDetailAddress).ConfigureAwait(true);
        vm.NotifyAddressCopied();
    }

    private void OnJoinLanServerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ViewModels.LanServerRowViewModel row } ||
            DataContext is not ViewModels.ServersViewModel vm)
        {
            return;
        }

        if (vm.JoinLanServerCommand.CanExecute(row.Address))
        {
            vm.JoinLanServerCommand.Execute(row.Address);
        }
    }

    private void OnJoinRecentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: string address } ||
            DataContext is not ViewModels.ServersViewModel vm)
        {
            return;
        }

        if (vm.JoinRecentCommand.CanExecute(address))
        {
            vm.JoinRecentCommand.Execute(address);
        }
    }
}
