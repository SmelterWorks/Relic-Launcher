using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace RelicLauncher.App.Views.Pages;

public partial class ServersPage : UserControl
{
    public ServersPage()
    {
        InitializeComponent();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ViewModels.ServersViewModel vm)
        {
            return;
        }

        e.Handled = true;
        if (vm.RefreshCatalogCommand.CanExecute(null))
        {
            vm.RefreshCatalogCommand.Execute(null);
        }
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

    private void OnJoinLanEndpointClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: string address } ||
            DataContext is not ViewModels.ServersViewModel vm)
        {
            return;
        }

        if (vm.JoinLanAddressCommand.CanExecute(address))
        {
            vm.JoinLanAddressCommand.Execute(address);
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
