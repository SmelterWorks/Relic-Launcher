using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using RelicLauncher.App.ViewModels;

namespace RelicLauncher.App.Views.Pages;

public partial class SettingsPage : UserControl
{
    private SettingsViewModel? _viewModel;

    public SettingsPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusAccountRequested -= OnFocusAccountRequested;
        }

        _viewModel = DataContext as SettingsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusAccountRequested += OnFocusAccountRequested;
        }
    }

    private void OnFocusAccountRequested(object? sender, EventArgs e)
        => Dispatcher.UIThread.Post(() => AccountEmailBox?.Focus());

    private void OnAccountFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not SettingsViewModel vm)
        {
            return;
        }

        if (vm.SignInCommand.CanExecute(null))
        {
            vm.SignInCommand.Execute(null);
            e.Handled = true;
        }
    }
}
