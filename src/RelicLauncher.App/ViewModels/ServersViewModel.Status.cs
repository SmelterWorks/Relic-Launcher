using CommunityToolkit.Mvvm.Input;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class ServersViewModel
{
    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedBrowseServer is null)
        {
            return;
        }

        var address = SelectedBrowseServer.Address;
        if (_favoriteAddresses.Contains(address))
        {
            var remove = await _favorites.RemoveAsync(address).ConfigureAwait(true);
            if (remove.IsSuccess)
            {
                _favoriteAddresses.Remove(address);
                SetStatus("Removed from favorites.");
            }
        }
        else
        {
            var entry = new FavoriteServerEntry
            {
                Name = SelectedBrowseServer.DisplayName,
                Address = address,
                SavedAt = DateTimeOffset.UtcNow,
            };
            var add = await _favorites.AddAsync(entry).ConfigureAwait(true);
            if (add.IsSuccess)
            {
                _favoriteAddresses.Add(address);
                SetStatus("Saved to favorites.");
            }
        }

        ApplyFiltersCore();
        OnPropertyChanged(nameof(IsSelectedFavorite));
        OnPropertyChanged(nameof(FavoriteButtonLabel));
        var refreshed = BrowseResults.FirstOrDefault(r =>
            string.Equals(r.Address, address, StringComparison.OrdinalIgnoreCase));
        if (refreshed is not null)
        {
            SelectedBrowseServer = refreshed;
        }
    }

    public bool IsSelectedFavorite =>
        SelectedBrowseServer is not null && _favoriteAddresses.Contains(SelectedBrowseServer.Address);

    public string FavoriteButtonLabel => IsSelectedFavorite ? "Unfavorite" : "Favorite";

    public void NotifyAddressCopied() => SetStatus("Address copied.");
}
