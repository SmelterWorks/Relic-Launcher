using CommunityToolkit.Mvvm.ComponentModel;

namespace RelicLauncher.App.ViewModels;

public abstract partial class PageViewModelBase : ViewModelBase
{
    [ObservableProperty]
    private string _statusMessage = string.Empty;
}
