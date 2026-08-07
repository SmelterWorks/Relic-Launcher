using CommunityToolkit.Mvvm.Input;
using RelicLauncher.App.Services;
using RelicLauncher.Core;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class AboutViewModel : PageViewModelBase
{
    private readonly IUrlLauncher _urlLauncher;
    private readonly LauncherUpdateCoordinator _updateCoordinator;
    private LauncherSettings _settings = new();
    private Action<LauncherSettings>? _onSettingsChanged;

    public AboutViewModel(
        IAppPathProvider pathProvider,
        IFileExplorerService fileExplorer,
        IUrlLauncher urlLauncher,
        LauncherUpdateCoordinator updateCoordinator)
    {
        _urlLauncher = urlLauncher;
        _updateCoordinator = updateCoordinator;
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

    public string ContactEmailDisplay => "team [at] smelterworks.com";

    public void Bind(LauncherSettings settings, Action<LauncherSettings>? onSettingsChanged = null)
    {
        _settings = settings;
        _onSettingsChanged = onSettingsChanged;
    }

    [RelayCommand]
    private void OpenContactEmail() => _urlLauncher.OpenUrl("mailto:team@smelterworks.com");

    [RelayCommand]
    private void OpenRepository() => _urlLauncher.OpenUrl("https://github.com/SmelterWorks/Relic-Launcher");

    [RelayCommand]
    private void OpenVintageStory() => _urlLauncher.OpenUrl("https://www.vintagestory.at/");

    [RelayCommand]
    private void OpenIssues() => _urlLauncher.OpenUrl("https://github.com/SmelterWorks/Relic-Launcher/issues");

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        SetStatus("Checking for updates...", isError: false);
        var outcome = await _updateCoordinator.CheckAndPromptAsync(_settings, _onSettingsChanged, force: true)
            .ConfigureAwait(true);
        SetStatus(outcome switch
        {
            LauncherUpdateCheckOutcome.UpToDate => "Relic Launcher is up to date.",
            LauncherUpdateCheckOutcome.UpdateAvailable => "An update is available.",
            LauncherUpdateCheckOutcome.Dismissed => "An update is available. You dismissed this version earlier.",
            LauncherUpdateCheckOutcome.Skipped => "Launcher updates are turned off in Settings.",
            LauncherUpdateCheckOutcome.Busy => "An update check is already running.",
            _ => string.Empty,
        }, isError: outcome == LauncherUpdateCheckOutcome.Failed);
    }
}
