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
    private async Task SearchAsync()
    {
        ApplyInstalledFilters();
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        Page = 1;
        await LoadPageAsync(token).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (!HasNextPage)
        {
            return;
        }

        Page++;
        await LoadPageAsync(CancellationToken.None).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (!HasPreviousPage)
        {
            return;
        }

        Page--;
        await LoadPageAsync(CancellationToken.None).ConfigureAwait(true);
    }

    private async Task LoadPageAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        ClearBrowseResults();
        HasBrowseResults = false;
        try
        {
            var orderBy = SelectedSortOption?.Id ?? "downloads";
            var orderDirection = string.Equals(orderBy, "name", StringComparison.OrdinalIgnoreCase)
                ? "asc"
                : "desc";
            var result = await _modDb.SearchAsync(new ModSearchQuery
            {
                Text = SearchText,
                GameVersion = FilterByActiveVersion ? _settings.SelectedVersion : null,
                OrderBy = orderBy,
                OrderDirection = orderDirection,
                Side = SelectedSideFilter?.Id,
                TagIds = _selectedTagIds.ToList(),
                TagNames = TagChips.Where(t => t.IsSelected).Select(t => t.Name).ToList(),
                Page = Page,
                PageSize = DefaultPageSize,
                PreferCache = true,
            }, cancellationToken).ConfigureAwait(true);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                ApplySearchFailure(result.Error);
                return;
            }

            ApplySearchSuccess(result.Value!);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsLoading = false;
            }
        }
    }

    private void ApplySearchFailure(string? error)
    {
        StatusMessage = error ?? "Mod search failed.";
        _logger.LogWarning("Mod search failed: {Error}", error);
        ClearBrowseResults();
        HasBrowseResults = false;
        EmptyBrowseMessage = "Could not load mods.";
        UpdatePaging(0, Page, DefaultPageSize);
    }

    private void ApplySearchSuccess(ModSearchResult page)
    {
        ClearBrowseResults();
        foreach (var mod in page.Mods)
        {
            BrowseResults.Add(new ModRowViewModel(mod, _images, OpenModAsync));
        }

        HasBrowseResults = BrowseResults.Count > 0;
        TotalCount = page.TotalCount;
        UpdatePaging(page.TotalCount, page.Page, page.PageSize);
        EmptyBrowseMessage = HasBrowseResults
            ? string.Empty
            : FilterByActiveVersion && !string.IsNullOrWhiteSpace(_settings.SelectedVersion)
                ? $"No mods matched for version {_settings.SelectedVersion}."
                : "No mods matched your search.";

        if (page.FromCache && string.IsNullOrWhiteSpace(StatusMessage) && TotalCount > 0)
        {
            StatusMessage = page.IsStale
                ? "Showing saved ModDB catalog while offline."
                : $"Showing {TotalCount:N0} mods.";
        }
    }

    private void UpdatePaging(int total, int page, int pageSize)
    {
        TotalCount = total;
        Page = page;
        PageSize = pageSize;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(1, pageSize)));
        PageLabel = total == 0 ? "No results" : $"Page {page} of {totalPages} ({total:N0})";
        HasPreviousPage = page > 1;
        HasNextPage = page * pageSize < total;
    }
}
