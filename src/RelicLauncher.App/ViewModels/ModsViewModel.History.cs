using CommunityToolkit.Mvvm.Input;

namespace RelicLauncher.App.ViewModels;

public partial class ModsViewModel
{
    private const int MaxSearchHistory = 8;

    public bool HasSearchHistory => SearchHistory.Count > 0;

    private void LoadSearchHistory()
    {
        SearchHistory.Clear();
        foreach (var term in _settings.ModSearchHistory ?? [])
        {
            if (!string.IsNullOrWhiteSpace(term))
            {
                SearchHistory.Add(term);
            }
        }

        OnPropertyChanged(nameof(HasSearchHistory));
    }

    private void RecordSearchHistory()
    {
        var trimmed = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        var list = _settings.ModSearchHistory?.ToList() ?? [];
        list.RemoveAll(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, trimmed);
        if (list.Count > MaxSearchHistory)
        {
            list = list.Take(MaxSearchHistory).ToList();
        }

        _settings.ModSearchHistory = list;
        LoadSearchHistory();
        _ = _settingsStore.SaveAsync(_settings);
    }

    [RelayCommand]
    private async Task SearchFromHistoryAsync(string term)
    {
        SearchText = term;
        await SearchAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ClearSearchHistoryAsync()
    {
        _settings.ModSearchHistory = [];
        SearchHistory.Clear();
        OnPropertyChanged(nameof(HasSearchHistory));
        await _settingsStore.SaveAsync(_settings).ConfigureAwait(true);
    }
}
