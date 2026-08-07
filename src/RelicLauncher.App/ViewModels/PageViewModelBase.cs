using CommunityToolkit.Mvvm.ComponentModel;

namespace RelicLauncher.App.ViewModels;

public abstract partial class PageViewModelBase : ViewModelBase
{
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _statusIsError;

    protected void SetStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }
}
