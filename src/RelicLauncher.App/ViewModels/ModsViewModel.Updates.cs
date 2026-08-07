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
    partial void OnHasAvailableUpdatesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUpdateAll));
    }

    partial void OnIsCheckingUpdatesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUpdateAll));
    }

    partial void OnUpdateStatusMessageChanged(string value) => OnPropertyChanged(nameof(ShowCheckForUpdates));
    private void ApplyUpdateStateToRows()
    {
        var recentlyUpdated = _updateState.GetRecentlyUpdatedMods();
        foreach (var row in _allInstalledRows)
        {
            var modId = row.Info.ModId ?? string.Empty;
            _updateCandidates.TryGetValue(modId, out var candidate);
            recentlyUpdated.TryGetValue(modId, out var updatedVersion);
            row.ApplyUpdateState(candidate, recentlyUpdated.ContainsKey(modId), updatedVersion);
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
        => await RunUpdateCheckAsync(force: true).ConfigureAwait(true);

    private async Task RunUpdateCheckAsync(bool force)
    {
        if (_settings.ModUpdateMode == ModUpdateMode.Off)
        {
            return;
        }

        var gameVersion = _settings.SelectedVersion;
        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            UpdateStatusMessage = "Set an active game version to check for mod updates.";
            return;
        }

        IsCheckingUpdates = true;
        try
        {
            var result = await _updateCheck.CheckForUpdatesAsync(
                ResolveDataPath(),
                gameVersion,
                _settings.ModUpdateOptOutModIds,
                force).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                UpdateStatusMessage = result.Error ?? "Could not check for mod updates.";
                return;
            }

            _updateCandidates.Clear();
            foreach (var candidate in result.Value!.Candidates)
            {
                _updateCandidates[candidate.ModId] = candidate;
            }

            HasAvailableUpdates = _updateCandidates.Count > 0;
            ApplyUpdateStateToRows();
            UpdateSelectedInstalledState();
            UpdateStatusMessage = BuildUpdateStatusMessage(result.Value);

            if (_settings.ModUpdateMode == ModUpdateMode.Automatic && HasAvailableUpdates)
            {
                await ApplyAutomaticUpdatesAsync(result.Value.Candidates).ConfigureAwait(true);
            }
        }
        finally
        {
            IsCheckingUpdates = false;
            _updateCheckScheduled = false;
        }
    }

    private static string BuildUpdateStatusMessage(ModUpdateCheckResult result)
        => result.Candidates.Count == 0
            ? "No mod updates found."
            : result.Candidates.Count == 1
                ? "1 update available."
                : $"{result.Candidates.Count} updates available.";

    private async Task ApplyAutomaticUpdatesAsync(IReadOnlyList<ModUpdateCandidate> candidates)
    {
        var updated = 0;
        foreach (var candidate in candidates)
        {
            if (!await UpdateCandidateAsync(candidate, confirmDependencies: false).ConfigureAwait(true))
            {
                continue;
            }

            updated++;
            _updateState.MarkRecentlyUpdated(candidate.ModId, candidate.AvailableVersion);
        }

        if (updated > 0)
        {
            UpdateStatusMessage = updated == 1
                ? "1 mod updated."
                : $"{updated} mods updated.";
            await RefreshInstalledAsync().ConfigureAwait(true);
            await RunUpdateCheckAsync(force: true).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task UpdateAllAsync()
    {
        if (!HasAvailableUpdates)
        {
            return;
        }

        var lines = _updateCandidates.Values
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => $"- {c.Name}: {FormatVersionLabel(c.InstalledVersion)} -> {FormatVersionLabel(c.AvailableVersion)}")
            .ToList();
        var confirmed = await _confirmDialog.ConfirmAsync(
            "Update all mods",
            $"Install these updates?{Environment.NewLine}{string.Join(Environment.NewLine, lines)}",
            "Update all",
            "Cancel").ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        var updated = 0;
        foreach (var candidate in _updateCandidates.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (await UpdateCandidateAsync(candidate, confirmDependencies: true).ConfigureAwait(true))
            {
                updated++;
            }
        }

        if (updated > 0)
        {
            StatusMessage = updated == 1
                ? "Updated 1 mod."
                : $"Updated {updated} mods.";
            await RefreshInstalledAsync().ConfigureAwait(true);
            await RunUpdateCheckAsync(force: true).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task UpdateInstalledModAsync(InstalledModRowViewModel? row)
    {
        if (row?.UpdateCandidate is null)
        {
            return;
        }

        row.ClearRecentlyUpdatedIndicator();
        if (!string.IsNullOrWhiteSpace(row.Info.ModId))
        {
            _updateState.ClearRecentlyUpdated(row.Info.ModId);
        }

        if (await UpdateCandidateAsync(row.UpdateCandidate, confirmDependencies: true).ConfigureAwait(true))
        {
            await RefreshInstalledAsync().ConfigureAwait(true);
            await RunUpdateCheckAsync(force: true).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task UpdateSelectedAsync()
    {
        if (SelectedDetails is null)
        {
            DetailStatus = "Select a mod first.";
            return;
        }

        var modId = ResolveModIdentifier(SelectedDetails);
        if (!_updateCandidates.TryGetValue(modId, out var candidate))
        {
            DetailStatus = "No update available for this mod.";
            return;
        }

        if (await UpdateCandidateAsync(candidate, confirmDependencies: true).ConfigureAwait(true))
        {
            await RefreshInstalledAsync().ConfigureAwait(true);
            await RunUpdateCheckAsync(force: true).ConfigureAwait(true);
            if (SelectedDetails is not null)
            {
                await OpenModByKeyAsync(ResolveModIdentifier(SelectedDetails)).ConfigureAwait(true);
            }
        }
    }

    [RelayCommand]
    private async Task ToggleSelectedModUpdateOptOutAsync()
    {
        if (SelectedDetails is null)
        {
            return;
        }

        var modId = ResolveModIdentifier(SelectedDetails);
        var optedOut = _settings.ModUpdateOptOutModIds
            .Any(id => string.Equals(id, modId, StringComparison.OrdinalIgnoreCase));
        if (optedOut)
        {
            for (var i = _settings.ModUpdateOptOutModIds.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_settings.ModUpdateOptOutModIds[i], modId, StringComparison.OrdinalIgnoreCase))
                {
                    _settings.ModUpdateOptOutModIds.RemoveAt(i);
                }
            }
        }
        else
        {
            _settings.ModUpdateOptOutModIds.Add(modId);
        }

        IsSelectedModOptedOut = !optedOut;
        await PersistSettingsAsync().ConfigureAwait(true);
        await RunUpdateCheckAsync(force: true).ConfigureAwait(true);
    }

    private async Task<bool> UpdateCandidateAsync(ModUpdateCandidate candidate, bool confirmDependencies)
    {
        if (!await _installOrchestrator.ConfirmBlockedReleaseAsync(_settings, candidate.ModId, candidate.Release)
                .ConfigureAwait(true))
        {
            return false;
        }

        if (confirmDependencies)
        {
            var plan = await BuildInstallPlanAsync(candidate.Release).ConfigureAwait(true);
            if (plan is null)
            {
                return false;
            }

            if (!await ConfirmDependencyPlanAsync(plan).ConfigureAwait(true))
            {
                return false;
            }

            if (!await ConfirmBlockedPlanAsync(plan).ConfigureAwait(true))
            {
                return false;
            }

            var planResult = await _installOrchestrator.InstallPlanAsync(
                ResolveDataPath(),
                plan,
                step => step.Depth == 0 ? candidate.Name : step.ModId).ConfigureAwait(true);
            return planResult.Success;
        }

        var result = await _installOrchestrator.InstallReleaseAsync(
            ResolveDataPath(),
            candidate.Name,
            candidate.Release).ConfigureAwait(true);
        return result.Success;
    }
}
