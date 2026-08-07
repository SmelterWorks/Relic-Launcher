using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;

namespace RelicLauncher.App.ViewModels;

public partial class ModsViewModel
{
    [RelayCommand]
    private void OpenModFolder(LocalModInfo? mod)
    {
        if (mod is null || string.IsNullOrWhiteSpace(mod.Path))
        {
            return;
        }

        var target = mod.IsDirectory || Directory.Exists(mod.Path)
            ? mod.Path
            : Path.GetDirectoryName(mod.Path);
        if (string.IsNullOrWhiteSpace(target))
        {
            SetStatus("Could not resolve mod folder.", true);
            return;
        }

        var result = _fileExplorer.OpenFolder(target);
        if (!result.IsSuccess)
        {
            SetStatus(result.Error ?? "Could not open mod folder.", true);
        }
    }

    [RelayCommand]
    private void OpenModDbPage()
    {
        if (SelectedDetails is null)
        {
            return;
        }

        var url = VintageStoryEndpoints.BuildModDbPageUrl(SelectedDetails.UrlAlias, SelectedDetails.ModId);
        _urlLauncher.OpenUrl(url);
    }

    private void UpdateTagFilterState()
    {
        HasSelectedTags = _selectedTagIds.Count > 0;
        OnPropertyChanged(nameof(TagsMenuLabel));
    }

    partial void OnSelectedReleaseChanged(ModReleaseInfo? value)
    {
        if (_ready && SelectedDetails is not null)
        {
            _ = RefreshBlocklistWarningAsync(SelectedDetails, value);
        }
    }

    [RelayCommand]
    private void OpenUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            _urlLauncher.OpenUrl(url);
        }
    }
}
