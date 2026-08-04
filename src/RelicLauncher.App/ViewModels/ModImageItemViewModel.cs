using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RelicLauncher.App.ViewModels;

public sealed partial class ModImageItemViewModel : ViewModelBase
{
    private readonly Func<ModImageItemViewModel, Task> _open;

    public ModImageItemViewModel(Bitmap thumbnail, string? fullUrl, Func<ModImageItemViewModel, Task> open)
    {
        Thumbnail = thumbnail;
        FullUrl = fullUrl;
        _open = open;
    }

    public Bitmap Thumbnail { get; }
    public string? FullUrl { get; }

    [RelayCommand]
    private Task OpenAsync() => _open(this);
}
