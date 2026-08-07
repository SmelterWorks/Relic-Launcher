using System.Collections.ObjectModel;

namespace RelicLauncher.App.ViewModels;

public sealed class ToastHostViewModel : ViewModelBase
{
    public ObservableCollection<ToastItemViewModel> Items { get; } = [];
}
