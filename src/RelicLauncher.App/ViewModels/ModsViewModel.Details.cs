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
    private async Task OpenModAsync(ModSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        var id = !string.IsNullOrWhiteSpace(summary.UrlAlias)
            ? summary.UrlAlias
            : summary.ModId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await OpenModByKeyAsync(id!).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenInstalledModAsync(InstalledModRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(row.Info.ModId))
        {
            _updateState.ClearRecentlyUpdated(row.Info.ModId);
            row.ClearRecentlyUpdatedIndicator();
        }

        if (row.Catalog is not null)
        {
            await OpenModAsync(row.Catalog).ConfigureAwait(true);
            return;
        }

        var id = NormalizeKey(row.Info.ModId);
        if (string.IsNullOrEmpty(id))
        {
            SelectedDetails = null;
            SelectedRelease = null;
            SetDetailStatus("This local mod is not linked to ModDB.", true);
            UpdateSelectedInstalledState();
            RefreshDependencyRowsForLocal(row.Info);
            return;
        }

        await OpenModByKeyAsync(id).ConfigureAwait(true);
    }

    private async Task OpenModByKeyAsync(string id)
    {
        _detailCts?.Cancel();
        _detailCts?.Dispose();
        _detailCts = new CancellationTokenSource();
        var token = _detailCts.Token;

        IsLoadingDetails = true;
        SetDetailStatus("Loading details...");
        SelectedDetails = null;
        SelectedRelease = null;
        CloseImageViewer();
        ClearScreenshotItems();
        DetailLogo = ModIconAssets.Default;
        UpdateSelectedInstalledState();

        try
        {
            var result = await _modDb.GetModAsync(id).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                SetDetailStatus(result.Error ?? "Could not load mod details.", true);
                _logger.LogWarning("Mod details failed for {Id}: {Error}", id, result.Error);
                return;
            }

            SelectedDetails = result.Value!;
            SelectedRelease = await SelectDefaultReleaseAsync(SelectedDetails).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            SetDetailStatus(string.Empty);
            RebuildDetailTags(SelectedDetails);
            UpdateSelectedInstalledState();
            await RefreshBlocklistWarningAsync(SelectedDetails, SelectedRelease).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            _ = LoadDetailMediaAsync(SelectedDetails, token);
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsLoadingDetails = false;
            }
        }
    }

    private void RebuildDetailTags(ModDetails details)
    {
        DetailTagNames.Clear();
        foreach (var tag in details.Tags)
        {
            DetailTagNames.Add(tag);
        }
    }

    private async Task RefreshBlocklistWarningAsync(ModDetails? details, ModReleaseInfo? release)
    {
        BlocklistWarning = string.Empty;
        if (!_settings.WarnOnBlockedMods || details is null)
        {
            return;
        }

        var modId = ResolveModIdentifier(details);
        var match = await _blocklist.FindMatchAsync(modId, release?.ModVersion).ConfigureAwait(true);
        if (!match.IsSuccess || match.Value is null)
        {
            return;
        }

        BlocklistWarning = string.IsNullOrWhiteSpace(match.Value.Reason)
            ? $"Blocked by Vintage Story list: {match.Value.Id}"
            : $"Blocked by Vintage Story list: {match.Value.Id}. {match.Value.Reason}";
    }

    private async Task<ModReleaseInfo?> SelectDefaultReleaseAsync(ModDetails details)
    {
        var gameVersion = _settings.SelectedVersion;
        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            return details.Releases.FirstOrDefault();
        }

        var identifier = ResolveModIdentifier(details);
        var resolved = await _releaseResolver.ResolveAsync(identifier, gameVersion).ConfigureAwait(true);
        if (resolved.IsSuccess)
        {
            var match = details.Releases.FirstOrDefault(r => r.FileId == resolved.Value!.FileId);
            return match ?? resolved.Value;
        }

        return ModReleaseSelector.SelectBest(details.Releases, gameVersion)
               ?? details.Releases.FirstOrDefault();
    }

    private static string ResolveModIdentifier(ModDetails details)
    {
        if (!string.IsNullOrWhiteSpace(details.UrlAlias))
        {
            return details.UrlAlias;
        }

        return details.ModId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
