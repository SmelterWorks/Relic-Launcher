using CommunityToolkit.Mvvm.ComponentModel;

namespace RelicLauncher.App.ViewModels;

public sealed partial class BackupVersionRowViewModel : ViewModelBase
{
    public BackupVersionRowViewModel(string version)
    {
        Version = version;
    }

    public string Version { get; }

    [ObservableProperty]
    private bool _isSelected;
}
