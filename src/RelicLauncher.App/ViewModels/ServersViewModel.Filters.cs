using CommunityToolkit.Mvvm.Input;

namespace RelicLauncher.App.ViewModels;

public partial class ServersViewModel
{
    public bool CanClearBrowseFilters =>
        !string.IsNullOrWhiteSpace(SearchText) ||
        FilterHasPlayers ||
        FilterNoPassword ||
        FilterNotWhitelisted ||
        FilterVanilla ||
        FilterFavoritesOnly ||
        !string.Equals(SelectedVersionFilter?.Id, "any", StringComparison.Ordinal);

    public string? EmptyBrowseActionText => ShowEmptyBrowse && _allServers.Count > 0 && CanClearBrowseFilters
        ? "Clear filters"
        : null;

    [RelayCommand]
    private void ClearBrowseFilters()
    {
        SearchText = string.Empty;
        FilterHasPlayers = false;
        FilterNoPassword = false;
        FilterNotWhitelisted = false;
        FilterVanilla = false;
        FilterFavoritesOnly = false;
        SelectedVersionFilter = VersionFilterOptions[0];
        ApplyFiltersCore();
        OnPropertyChanged(nameof(CanClearBrowseFilters));
        OnPropertyChanged(nameof(EmptyBrowseActionText));
    }

    partial void OnSearchTextChanged(string value)
    {
        ScheduleApplyFilters();
        OnPropertyChanged(nameof(CanClearBrowseFilters));
        OnPropertyChanged(nameof(EmptyBrowseActionText));
    }

    partial void OnFilterHasPlayersChanged(bool value) => NotifyBrowseFilterChanged();

    partial void OnFilterNoPasswordChanged(bool value) => NotifyBrowseFilterChanged();

    partial void OnFilterNotWhitelistedChanged(bool value) => NotifyBrowseFilterChanged();

    partial void OnFilterVanillaChanged(bool value) => NotifyBrowseFilterChanged();

    partial void OnFilterFavoritesOnlyChanged(bool value) => NotifyBrowseFilterChanged();

    partial void OnSelectedVersionFilterChanged(ServerVersionFilterOption? value) => NotifyBrowseFilterChanged();

    private void NotifyBrowseFilterChanged()
    {
        ScheduleApplyFilters();
        OnPropertyChanged(nameof(CanClearBrowseFilters));
        OnPropertyChanged(nameof(EmptyBrowseActionText));
    }
}
