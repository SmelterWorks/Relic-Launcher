using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicLauncher.App.Services;

namespace RelicLauncher.App.ViewModels;

public partial class ToastActionItemViewModel : ViewModelBase
{
    public ToastActionItemViewModel(string label, Func<Task> handler, bool dismissOnClick, Action dismissToast)
    {
        Label = label;
        _handler = handler;
        _dismissOnClick = dismissOnClick;
        _dismissToast = dismissToast;
    }

    private readonly Func<Task> _handler;
    private readonly bool _dismissOnClick;
    private readonly Action _dismissToast;

    public string Label { get; }

    [RelayCommand]
    private async Task InvokeAsync()
    {
        await _handler().ConfigureAwait(true);
        if (_dismissOnClick)
        {
            _dismissToast();
        }
    }
}
