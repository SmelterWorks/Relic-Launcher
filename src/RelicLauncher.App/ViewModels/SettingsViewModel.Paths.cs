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
    private async Task BrowseInstallsRootAsync()
    {
        var path = await _storagePicker.PickFolderAsync("Select installs root folder").ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            InstallsRoot = path;
        }
    }

    [RelayCommand]
    private async Task BrowseDataPathAsync()
    {
        var path = await _storagePicker.PickFolderAsync("Select Vintage Story data folder").ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            DataPath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseCustomLogoPathAsync()
    {
        var path = await _storagePicker.PickImageFileAsync("Select home background logo").ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            HomeBackgroundCustomLogoPath = path;
        }
    }
}
