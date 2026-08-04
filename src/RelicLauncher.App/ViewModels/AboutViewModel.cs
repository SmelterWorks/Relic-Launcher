using CommunityToolkit.Mvvm.Input;
using RelicLauncher.Core;
using RelicLauncher.Core.Abstractions;

namespace RelicLauncher.App.ViewModels;

public partial class AboutViewModel : PageViewModelBase
{
    private readonly IUrlLauncher _urlLauncher;

    public AboutViewModel(IAppPathProvider pathProvider, IFileExplorerService fileExplorer, IUrlLauncher urlLauncher)
    {
        _urlLauncher = urlLauncher;
        Version = BuildMetadata.Version;
        CommitSha = BuildMetadata.CommitSha;
        BuildTimeUtc = BuildMetadata.BuildTimeUtc;
        LogsFolder = new FolderPathRowViewModel(fileExplorer);
        LogsFolder.Bind("Logs", pathProvider.GetPaths().LogsDirectory);
    }

    public string Title => "About Relic Launcher";
    public string Version { get; }
    public string CommitSha { get; }
    public string BuildTimeUtc { get; }
    public FolderPathRowViewModel LogsFolder { get; }
    public string Disclaimer =>
        "Relic Launcher is an unofficial community project. It is not affiliated with Anego Studios or Vintage Story.";

    [RelayCommand]
    private void OpenRepository() => _urlLauncher.OpenUrl("https://github.com/SmelterWorks/Relic-Launchder");

    [RelayCommand]
    private void OpenVintageStory() => _urlLauncher.OpenUrl("https://www.vintagestory.at/");

    [RelayCommand]
    private void OpenIssues() => _urlLauncher.OpenUrl("https://github.com/SmelterWorks/Relic-Launchder/issues");
}
