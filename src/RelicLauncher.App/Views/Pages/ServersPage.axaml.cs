using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace RelicLauncher.App.Views.Pages;

public partial class ServersPage : UserControl
{
    public ServersPage()
    {
        InitializeComponent();
    }

    private void OnBrowseSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ViewModels.ServersViewModel vm)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            vm.SearchText = string.Empty;
            e.Handled = true;
        }
    }

    private void OnDirectKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ViewModels.ServersViewModel vm)
        {
            return;
        }

        if (vm.DirectConnectCommand.CanExecute(null))
        {
            vm.DirectConnectCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnBrowseServerDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ViewModels.ServersViewModel vm)
        {
            return;
        }

        if (vm.JoinSelectedCommand.CanExecute(null))
        {
            vm.JoinSelectedCommand.Execute(null);
            e.Handled = true;
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
