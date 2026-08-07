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
    private async Task RefreshInstalledAsync()
    {
        InstalledMods.Clear();
        _allInstalledRows.Clear();
        _installedModInfos.Clear();
        var result = await _modLibrary.ListInstalledAsync(ResolveDataPath()).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            SetStatus(result.Error ?? "Could not list installed mods.", true);
            HasInstalledMods = false;
            HasDuplicateMods = false;
            DuplicateModsMessage = string.Empty;
            EmptyInstalledMessage = "Could not list installed mods.";
            UpdateSelectedInstalledState();
            return;
        }

        var catalog = await LoadCatalogIndexAsync().ConfigureAwait(true);
        var installed = result.Value!;
        var audit = ModDependencyResolver.Audit(installed, _settings.SelectedVersion);
        foreach (var mod in installed)
        {
            ModSummary? summary = null;
            if (!string.IsNullOrWhiteSpace(mod.ModId))
            {
                catalog.TryGetValue(NormalizeKey(mod.ModId), out summary);
            }

            IReadOnlyList<ModDependencyIssue>? issues = null;
            if (!string.IsNullOrWhiteSpace(mod.ModId)
                && audit.IssuesByDependentModId.TryGetValue(mod.ModId, out var blocking))
            {
                issues = blocking;
            }

            _allInstalledRows.Add(new InstalledModRowViewModel(mod, summary, _images, _modLibrary, issues));
            _installedModInfos.Add(mod);
        }

        UpdateDuplicateState();
        ApplyInstalledFilters();
        ApplyUpdateStateToRows();
        UpdateSelectedInstalledState();
        RefreshDependencyRowsForSelection();
    }

    private async Task<Dictionary<string, ModSummary>> LoadCatalogIndexAsync()
    {
        var index = new Dictionary<string, ModSummary>(StringComparer.OrdinalIgnoreCase);
        var result = await _modDb.GetCatalogAsync(preferCache: true).ConfigureAwait(true);
        if (!result.IsSuccess || result.Value is null)
        {
            return index;
        }

        foreach (var mod in result.Value)
        {
            index[mod.ModId.ToString(System.Globalization.CultureInfo.InvariantCulture)] = mod;
            if (!string.IsNullOrWhiteSpace(mod.UrlAlias))
            {
                index[mod.UrlAlias] = mod;
            }
        }

        return index;
    }

    private void ApplyInstalledFilters()
    {
        InstalledMods.Clear();
        var filtered = FilterInstalledRows(_allInstalledRows);
        foreach (var row in SortInstalledRows(filtered))
        {
            InstalledMods.Add(row);
        }

        HasInstalledMods = InstalledMods.Count > 0;
        EmptyInstalledMessage = _allInstalledRows.Count == 0
            ? "No mods installed in the data folder yet."
            : HasInstalledMods
                ? string.Empty
                : "No installed mods match the current search or filters.";
    }

    private IEnumerable<InstalledModRowViewModel> FilterInstalledRows(IEnumerable<InstalledModRowViewModel> source)
    {
        var text = SearchText?.Trim() ?? string.Empty;
        var side = SelectedSideFilter?.Id ?? "any";
        var selectedTagNames = TagChips
            .Where(t => t.IsSelected)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IEnumerable<InstalledModRowViewModel> query = source;
        if (!string.IsNullOrWhiteSpace(text))
        {
            query = query.Where(m =>
                ContainsIgnoreCase(m.Name, text) ||
                ContainsIgnoreCase(m.FileName, text) ||
                ContainsIgnoreCase(m.Version, text) ||
                ContainsIgnoreCase(m.Info.ModId, text) ||
                ContainsIgnoreCase(m.TagsLabel, text));
        }

        if (!string.Equals(side, "any", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(m => MatchesSideFilter(m.Side, side));
        }

        if (selectedTagNames.Count > 0)
        {
            query = query.Where(m => m.Tags.Any(tag => selectedTagNames.Contains(tag)));
        }

        return query;
    }

    private IEnumerable<InstalledModRowViewModel> SortInstalledRows(IEnumerable<InstalledModRowViewModel> source)
        => (SelectedSortOption?.Id ?? "name") switch
        {
            "name" => source.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
            "downloads" => source.OrderByDescending(m => m.Downloads)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
            "updated" => source.OrderByDescending(m => m.LastReleased ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
            "follows" or "trending" => source.OrderByDescending(m => m.Downloads)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
            _ => source.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
        };

    private static bool MatchesSideFilter(string side, string filter)
        => string.Equals(side, filter, StringComparison.OrdinalIgnoreCase) ||
           (string.Equals(filter, "client", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(side, "both", StringComparison.OrdinalIgnoreCase)) ||
           (string.Equals(filter, "server", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(side, "both", StringComparison.OrdinalIgnoreCase));

    private void UpdateDuplicateState()
    {
        var duplicateIds = 0;
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _allInstalledRows)
        {
            if (string.IsNullOrWhiteSpace(row.Info.ModId))
            {
                continue;
            }

            counts.TryGetValue(row.Info.ModId, out var count);
            counts[row.Info.ModId] = count + 1;
        }

        foreach (var count in counts.Values)
        {
            if (count > 1)
            {
                duplicateIds++;
            }
        }

        HasDuplicateMods = duplicateIds > 0;
        DuplicateModsMessage = HasDuplicateMods
            ? $"{duplicateIds} mod id(s) have multiple installs. Clean duplicates to keep one zip per mod."
            : string.Empty;
    }

    private void UpdateSelectedInstalledState()
    {
        if (SelectedDetails is null)
        {
            IsSelectedModInstalled = false;
            SelectedInstalledLabel = string.Empty;
            HasSelectedModUpdate = false;
            SelectedUpdateLabel = string.Empty;
            IsSelectedModOptedOut = false;
            return;
        }

        var modId = ResolveModIdentifier(SelectedDetails);
        var match = _allInstalledRows.FirstOrDefault(row => MatchesInstalled(row.Info, SelectedDetails));
        IsSelectedModInstalled = match is not null;
        SelectedInstalledLabel = match is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(match.Version)
                ? "Already installed"
                : $"Already installed ({match.Version})";
        HasSelectedModUpdate = _updateCandidates.ContainsKey(modId);
        if (HasSelectedModUpdate && _updateCandidates.TryGetValue(modId, out var candidate))
        {
            SelectedUpdateLabel = string.IsNullOrWhiteSpace(candidate.AvailableVersion)
                ? "Update available"
                : $"Update to {candidate.AvailableVersion}";
            if (candidate.Release.FileId > 0)
            {
                SelectedRelease = candidate.Release;
            }
        }
        else
        {
            SelectedUpdateLabel = string.Empty;
        }

        IsSelectedModOptedOut = _settings.ModUpdateOptOutModIds
            .Any(id => string.Equals(id, modId, StringComparison.OrdinalIgnoreCase));
        RefreshDependencyRowsForSelection();
    }

    private void RefreshDependencyRowsForSelection()
    {
        DependencyRows.Clear();
        LocalModInfo? local = null;
        if (SelectedDetails is not null)
        {
            local = _allInstalledRows.FirstOrDefault(row => MatchesInstalled(row.Info, SelectedDetails))?.Info;
        }

        if (local is null || local.Dependencies.Count == 0)
        {
            HasDependencyRows = false;
            return;
        }

        var audit = ModDependencyResolver.AuditMod(
            local,
            _installedModInfos,
            _settings.SelectedVersion);
        foreach (var issue in audit.Issues.Where(i =>
                     string.Equals(i.DependentModId, local.ModId, StringComparison.OrdinalIgnoreCase)))
        {
            DependencyRows.Add(new ModDependencyStatusRowViewModel(issue));
        }

        HasDependencyRows = DependencyRows.Count > 0;
    }

    private void RefreshDependencyRowsForLocal(LocalModInfo local)
    {
        DependencyRows.Clear();
        if (local.Dependencies.Count == 0)
        {
            HasDependencyRows = false;
            return;
        }

        var audit = ModDependencyResolver.AuditMod(
            local,
            _installedModInfos,
            _settings.SelectedVersion);
        foreach (var issue in audit.Issues.Where(i =>
                     string.Equals(i.DependentModId, local.ModId, StringComparison.OrdinalIgnoreCase)))
        {
            DependencyRows.Add(new ModDependencyStatusRowViewModel(issue));
        }

        HasDependencyRows = DependencyRows.Count > 0;
    }

    private static bool MatchesInstalled(LocalModInfo local, ModDetails details)
    {
        if (string.IsNullOrWhiteSpace(local.ModId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(details.UrlAlias) &&
            string.Equals(local.ModId, details.UrlAlias, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
            local.ModId,
            details.ModId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static bool ContainsIgnoreCase(string? haystack, string needle)
        => !string.IsNullOrWhiteSpace(haystack) &&
           haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
