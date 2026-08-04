using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicLauncher.Core.Abstractions;

namespace RelicLauncher.App.ViewModels;

public sealed partial class FolderPathRowViewModel : ViewModelBase
{
    private readonly IFileExplorerService _fileExplorer;

    public FolderPathRowViewModel(IFileExplorerService fileExplorer)
    {
        _fileExplorer = fileExplorer;
    }

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public void Bind(string label, string path)
    {
        Label = label;
        Path = path;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void OpenFolder()
    {
        var result = _fileExplorer.OpenFolder(Path);
        if (!result.IsSuccess)
        {
            StatusMessage = result.Error ?? "Could not open folder.";
        }
    }
}
