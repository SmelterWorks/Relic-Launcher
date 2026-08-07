using CommunityToolkit.Mvvm.ComponentModel;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class SettingsViewModel
{
    private DateTimeOffset? _lastLauncherUpdateCheckUtc;
    private string? _dismissedLauncherUpdateVersion;
    private string? _lastUpdateManifestEtag;

    [ObservableProperty]
    private LauncherUpdateMode _launcherUpdateMode = LauncherUpdateMode.Prompt;

    [ObservableProperty]
    private LauncherUpdateModeOption? _selectedLauncherUpdateModeOption;

    [ObservableProperty]
    private LauncherUpdateChannelOption? _selectedLauncherUpdateChannelOption;

    public IReadOnlyList<LauncherUpdateModeOption> LauncherUpdateModeOptions { get; } =
    [
        new LauncherUpdateModeOption
        {
            Mode = LauncherUpdateMode.Off,
            Label = "Off",
            Description = "Do not check for Relic Launcher updates.",
        },
        new LauncherUpdateModeOption
        {
            Mode = LauncherUpdateMode.Prompt,
            Label = "Prompt",
            Description = "Check for updates and show a toast when a newer build is available.",
        },
    ];

    public IReadOnlyList<LauncherUpdateChannelOption> LauncherUpdateChannelOptions { get; } =
    [
        new LauncherUpdateChannelOption
        {
            Channel = LauncherUpdateChannel.Stable,
            Label = "Stable",
            Description = "Follow tagged stable releases.",
        },
        new LauncherUpdateChannelOption
        {
            Channel = LauncherUpdateChannel.Nightly,
            Label = "Nightly",
            Description = "Follow nightly prerelease builds.",
        },
    ];

    partial void OnSelectedLauncherUpdateModeOptionChanged(LauncherUpdateModeOption? value)
    {
        if (value is not null)
        {
            LauncherUpdateMode = value.Mode;
            if (!_isBinding)
            {
                ScheduleAutoSave();
            }
        }
    }

    partial void OnSelectedLauncherUpdateChannelOptionChanged(LauncherUpdateChannelOption? value)
    {
        if (value is not null && !_isBinding)
        {
            ScheduleAutoSave();
        }
    }

    partial void OnLauncherUpdateModeChanged(LauncherUpdateMode value) => ScheduleAutoSave();

    private void ApplyLauncherUpdateSettings(LauncherSettings settings)
    {
        settings.LauncherUpdateMode = SelectedLauncherUpdateModeOption?.Mode ?? LauncherUpdateMode.Prompt;
        settings.LauncherUpdateChannel = SelectedLauncherUpdateChannelOption?.Channel ?? LauncherUpdateChannel.Stable;
        settings.LastLauncherUpdateCheckUtc = _lastLauncherUpdateCheckUtc;
        settings.DismissedLauncherUpdateVersion = _dismissedLauncherUpdateVersion;
        settings.LastUpdateManifestEtag = _lastUpdateManifestEtag;
    }
}
