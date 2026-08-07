using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class ServersViewModel
{
    [RelayCommand]
    private async Task RefreshCatalogAsync() => await LoadCatalogAsync(forceNetwork: true).ConfigureAwait(true);

    [RelayCommand]
    private async Task RetryCatalogAsync() => await LoadCatalogAsync(forceNetwork: true).ConfigureAwait(true);

    [RelayCommand]
    private void SelectBrowseTab() => SelectedTabIndex = 0;

    [RelayCommand]
    private void SelectDirectTab() => SelectedTabIndex = 1;

    [RelayCommand]
    private void SelectLanTab() => SelectedTabIndex = 2;

    public bool IsBrowseTab => SelectedTabIndex == 0;
    public bool IsDirectTab => SelectedTabIndex == 1;
    public bool IsLanTab => SelectedTabIndex == 2;

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsBrowseTab));
        OnPropertyChanged(nameof(IsDirectTab));
        OnPropertyChanged(nameof(IsLanTab));

        if (value == 2)
        {
            RestartLanAutoRefresh();
        }
        else
        {
            StopLanAutoRefresh();
        }
    }

    private async Task LoadCatalogAsync(bool forceNetwork)
    {
        IsLoading = true;
        ShowCatalogError = false;
        CatalogErrorMessage = string.Empty;
        SetStatus(string.Empty);
        try
        {
            var result = await _masterServerClient.FetchCatalogAsync(!forceNetwork).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                ApplyCatalogFailure(result.Error);
                return;
            }

            ApplyCatalogSuccess(result.Value!);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyCatalogFailure(string? error)
    {
        _allServers = [];
        BrowseResults.Clear();
        HasBrowseResults = false;
        ShowEmptyBrowse = false;
        ShowCatalogError = true;
        CatalogErrorMessage = error ?? "Could not load the public server list.";
        SetStatus(CatalogErrorMessage, true);
        _logger.LogWarning("Server catalog load failed: {Error}", error);
    }

    private void ApplyCatalogSuccess(MasterServerFetchResult fetch)
    {
        _allServers = fetch.Catalog.Servers;
        ApplyFiltersCore();

        if (fetch.FromCache && fetch.IsStale)
        {
            var local = fetch.Catalog.FetchedAt.ToLocalTime();
            SetStatus($"Showing saved server list from {local:g}. Live list unavailable.");
        }
        else if (fetch.UsedOfficialFallback)
        {
            SetStatus($"Loaded {fetch.Catalog.Servers.Count:N0} servers from the official catalog.");
        }
        else if (!fetch.FromCache)
        {
            SetStatus($"Loaded {fetch.Catalog.Servers.Count:N0} public servers.");
        }
    }

    private void ScheduleApplyFilters()
    {
        _filterCts?.Cancel();
        _filterCts = new CancellationTokenSource();
        var token = _filterCts.Token;
        var generation = Interlocked.Increment(ref _filterGeneration);
        _ = ApplyFiltersDelayedAsync(generation, token);
    }

    private async Task ApplyFiltersDelayedAsync(int generation, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(200, cancellationToken).ConfigureAwait(true);
            if (generation != _filterGeneration || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ApplyFiltersCore();
        }
        catch (OperationCanceledException)
        {
        }
    }
}
