using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class SettingsViewModel
{
    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        var confirmed = await _confirmDialog.ConfirmAsync(
            "Reset settings",
            "Restore all launcher settings to defaults? Your account session is kept.",
            "Reset",
            "Cancel").ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        _isBinding = true;
        var platform = _platform.GetPlatformInfo();
        InstallsRoot = platform.DefaultInstallsRoot;
        DataPath = platform.DefaultDataPath;
        SelectedVersion = string.Empty;
        ConfirmBeforeExit = false;
        WarnOnBlockedMods = true;
        ModUpdateMode = ModUpdateMode.Prompt;
        SelectedModUpdateModeOption = ModUpdateModeOptions.FirstOrDefault(o => o.Mode == ModUpdateMode.Prompt);
        _modUpdateOptOutModIds = [];
        HomeBackgroundLogoMode = HomeBackgroundLogoMode.Square;
        SelectedLogoModeOption = LogoModeOptions.FirstOrDefault(o => o.Mode == HomeBackgroundLogoMode.Square);
        HomeBackgroundCustomLogoPath = string.Empty;
        HomeBackgroundLogoOpacity = RelicDefaults.HomeBackgroundLogoOpacity;
        BindEndpoints(EndpointSettings.CreateDefaults());
        SelectedTheme = Themes.FirstOrDefault(t => string.Equals(t.Id, LauncherSettings.DefaultThemeId, StringComparison.OrdinalIgnoreCase))
            ?? Themes.FirstOrDefault();
        AccountError = string.Empty;
        StatusMessage = "Settings restored to defaults.";
        _isBinding = false;
        await PersistSettingsAsync().ConfigureAwait(true);
        SetSaveStatus("Saved defaults");
    }

    [RelayCommand]
    private void ResetEndpointUrls()
    {
        if (_isBinding)
        {
            return;
        }

        _isBinding = true;
        BindEndpoints(EndpointSettings.CreateDefaults());
        _isBinding = false;
        ScheduleAutoSave();
        StatusMessage = "Endpoint URLs restored to defaults.";
    }
}
