using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class ServersViewModel
{
    private void ApplyFiltersCore()
    {
        lock (_filterGate)
        {
            ApplyFiltersCoreUnsynchronized();
        }
    }

    private void ApplyFiltersCoreUnsynchronized()
    {
        if (_allServers.Count == 0)
        {
            BrowseResults.Clear();
            HasBrowseResults = false;
            ShowEmptyBrowse = !ShowCatalogError;
            EmptyBrowseMessage = ShowCatalogError
                ? string.Empty
                : "No public servers are listed right now.";
            return;
        }

        var list = SortServers(_allServers.Where(MatchesFilters)).ToList();
        BrowseResults.Clear();
        foreach (var server in list)
        {
            BrowseResults.Add(new ServerRowViewModel(
                server,
                _favoriteAddresses.Contains(server.ServerAddress)));
        }

        HasBrowseResults = BrowseResults.Count > 0;
        ShowEmptyBrowse = !HasBrowseResults && !ShowCatalogError;
        EmptyBrowseMessage = HasBrowseResults
            ? string.Empty
            : "No servers match your filters. Try clearing search or toggles.";
    }

    private bool MatchesFilters(PublicServerSummary server)
    {
        if (FilterFavoritesOnly && !_favoriteAddresses.Contains(server.ServerAddress))
        {
            return false;
        }

        if (FilterHasPlayers && server.Players <= 0)
        {
            return false;
        }

        if (FilterNoPassword && server.HasPassword)
        {
            return false;
        }

        if (FilterNotWhitelisted && server.Whitelisted)
        {
            return false;
        }

        if (FilterVanilla && server.ModCount > 0)
        {
            return false;
        }

        var query = SearchText.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var matchName = server.ServerName.Contains(query, StringComparison.OrdinalIgnoreCase);
            var matchAddress = server.ServerAddress.Contains(query, StringComparison.OrdinalIgnoreCase);
            var matchDescription = server.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
            if (!matchName && !matchAddress && !matchDescription)
            {
                return false;
            }
        }

        return MatchesVersionFilter(server);
    }

    private bool MatchesVersionFilter(PublicServerSummary server)
    {
        var versionFilter = SelectedVersionFilter?.Id ?? "any";
        var activeVersion = _settings.SelectedVersion;
        if (string.Equals(versionFilter, "active", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(activeVersion))
        {
            return string.Equals(server.GameVersion, activeVersion, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(versionFilter, "compatible", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(activeVersion) &&
            !string.IsNullOrWhiteSpace(server.GameVersion))
        {
            var activeMajor = activeVersion.Split('.').FirstOrDefault();
            var serverMajor = server.GameVersion.Split('.').FirstOrDefault();
            return string.Equals(activeMajor, serverMajor, StringComparison.Ordinal);
        }

        return true;
    }

    private IEnumerable<PublicServerSummary> SortServers(IEnumerable<PublicServerSummary> servers)
    {
        var sortId = SelectedSortOption?.Id ?? "players-desc";
        return sortId switch
        {
            "players-asc" => servers.OrderBy(s => s.Players).ThenBy(s => s.ServerName, StringComparer.OrdinalIgnoreCase),
            "name" => servers.OrderBy(s => s.ServerName, StringComparer.OrdinalIgnoreCase),
            "version" => servers.OrderBy(s => s.GameVersion ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(s => s.Players),
            _ => servers.OrderByDescending(s => s.Players).ThenBy(s => s.ServerName, StringComparer.OrdinalIgnoreCase),
        };
    }
}
