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
    private async Task LoadTagsAsync()
    {
        var result = await _modDb.GetTagsAsync().ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            _logger.LogDebug("ModDB tags unavailable: {Error}", result.Error);
            return;
        }

        _allTags = result.Value!;
        RebuildTagChips();
    }

    private void RebuildTagChips()
    {
        TagChips.Clear();
        foreach (var tag in _allTags)
        {
            TagChips.Add(new ModTagChipViewModel(
                tag,
                _selectedTagIds.Contains(tag.TagId),
                OnTagChipToggled));
        }

        UpdateSelectedTagsLabel();
    }

    private void OnTagChipToggled(ModTagChipViewModel chip)
    {
        if (_selectedTagIds.Contains(chip.TagId))
        {
            _selectedTagIds.Remove(chip.TagId);
            chip.IsSelected = false;
        }
        else
        {
            _selectedTagIds.Add(chip.TagId);
            chip.IsSelected = true;
        }

        UpdateSelectedTagsLabel();
        if (_ready)
        {
            _ = SearchAsync();
        }
    }

    [RelayCommand]
    private void ToggleDetailTag(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return;
        }

        var match = _allTags.FirstOrDefault(t =>
            string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return;
        }

        var chip = TagChips.FirstOrDefault(c =>
            string.Equals(c.TagId, match.TagId, StringComparison.OrdinalIgnoreCase));
        if (chip is not null)
        {
            OnTagChipToggled(chip);
            return;
        }

        if (_selectedTagIds.Contains(match.TagId))
        {
            _selectedTagIds.Remove(match.TagId);
        }
        else
        {
            _selectedTagIds.Add(match.TagId);
        }

        RebuildTagChips();
        if (_ready)
        {
            _ = SearchAsync();
        }
    }

    [RelayCommand]
    private void ClearSelectedTags()
    {
        if (_selectedTagIds.Count == 0)
        {
            return;
        }

        _selectedTagIds.Clear();
        foreach (var chip in TagChips)
        {
            chip.IsSelected = false;
        }

        UpdateSelectedTagsLabel();
        if (_ready)
        {
            _ = SearchAsync();
        }
    }
}
