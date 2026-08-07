using RelicLauncher.Core.Models;
using RelicLauncher.Core.Security;
using RelicLauncher.Infrastructure.Server;

namespace RelicLauncher.App.ViewModels;

public partial class ServersViewModel
{
    private void RefreshLocalServerState()
    {
        LocalServerRunning = _serverHost.State == ServerProcessState.Running;
        LocalListenEndpoints.Clear();
        if (!LocalServerRunning)
        {
            return;
        }

        var dataPath = _settings.ServerDataPath ?? _platform.GetPlatformInfo().DefaultServerDataPath;
        foreach (var endpoint in ServerListenEndpointResolver.Resolve(dataPath))
        {
            LocalListenEndpoints.Add(endpoint);
        }
    }

    private void UpdateSelectedDetail()
    {
        if (SelectedBrowseServer is null)
        {
            SelectedDetailDescription = string.Empty;
            SelectedDetailAddress = string.Empty;
            SelectedDetailMeta = string.Empty;
            VersionMismatchWarning = string.Empty;
            return;
        }

        var server = SelectedBrowseServer.Source;
        SelectedDetailDescription = SelectedBrowseServer.DisplayDescription;
        SelectedDetailAddress = server.ServerAddress;
        SelectedDetailMeta = $"{server.Players}/{server.MaxPlayers} players · {server.GameVersion ?? "unknown"} · {SelectedBrowseServer.ModCountLabel}";
        if (server.HasPassword)
        {
            SelectedDetailMeta += " · Password";
        }

        if (server.Whitelisted)
        {
            SelectedDetailMeta += " · Whitelist";
        }

        var active = _settings.SelectedVersion;
        if (!string.IsNullOrWhiteSpace(active) && !string.IsNullOrWhiteSpace(server.GameVersion) &&
            !string.Equals(active, server.GameVersion, StringComparison.OrdinalIgnoreCase))
        {
            VersionMismatchWarning =
                $"Server reports {server.GameVersion}. Your active version is {active}. Join may fail or prompt mod download in-game.";
        }
        else
        {
            VersionMismatchWarning = string.Empty;
        }

        OnPropertyChanged(nameof(IsSelectedFavorite));
        OnPropertyChanged(nameof(FavoriteButtonLabel));
    }

    private async Task LoadFavoritesAndRecentsAsync()
    {
        var favorites = await _favorites.ListAsync().ConfigureAwait(true);
        if (favorites.IsSuccess)
        {
            _favoriteAddresses = favorites.Value!
                .Select(f => f.Address)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        await LoadRecentsAsync().ConfigureAwait(true);
        if (_allServers.Count > 0)
        {
            ApplyFiltersCore();
        }
    }

    private async Task LoadRecentsAsync()
    {
        var recents = await _recents.ListAsync().ConfigureAwait(true);
        RecentAddresses.Clear();
        if (recents.IsSuccess)
        {
            foreach (var address in recents.Value!)
            {
                RecentAddresses.Add(address);
            }
        }
    }
}
