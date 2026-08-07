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
    private async Task InstallSelectedAsync()
    {
        if (SelectedDetails is null)
        {
            SetDetailStatus("Select a mod first.", true);
            return;
        }

        if (IsSelectedModInstalled && !HasSelectedModUpdate)
        {
            SetDetailStatus(SelectedInstalledLabel);
            return;
        }

        if (HasSelectedModUpdate)
        {
            await UpdateSelectedAsync().ConfigureAwait(true);
            return;
        }

        var release = SelectedRelease ?? SelectedDetails.Releases.FirstOrDefault();
        if (release is null)
        {
            SetDetailStatus("No releases available.", true);
            return;
        }

        if (!await ConfirmBlockedInstallAsync(SelectedDetails, release).ConfigureAwait(true))
        {
            return;
        }

        var plan = await BuildInstallPlanAsync(release).ConfigureAwait(true);
        if (plan is null)
        {
            return;
        }

        if (!await ConfirmDependencyPlanAsync(plan).ConfigureAwait(true))
        {
            return;
        }

        if (!await ConfirmBlockedPlanAsync(plan).ConfigureAwait(true))
        {
            return;
        }

        await RunModInstallPlanAsync(plan).ConfigureAwait(true);
    }

    private async Task<ModDependencyInstallPlan?> BuildInstallPlanAsync(ModReleaseInfo release)
    {
        var installed = _allInstalledRows.Select(r => r.Info).ToList();
        var gameVersion = _settings.SelectedVersion;
        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            SetDetailStatus("Set an active game version before installing with dependencies.", true);
            return null;
        }

        SetDetailStatus("Resolving dependencies...");
        var planResult = await _dependencyPlanner.PlanAsync(release, gameVersion, installed).ConfigureAwait(true);
        if (!planResult.IsSuccess)
        {
            SetDetailStatus(planResult.Error ?? "Could not resolve dependencies.", true);
            return null;
        }

        return planResult.Value;
    }

    private async Task<bool> ConfirmBlockedPlanAsync(ModDependencyInstallPlan plan)
        => await _installOrchestrator.ConfirmBlockedPlanAsync(_settings, plan).ConfigureAwait(true);

    private async Task<bool> ConfirmDependencyPlanAsync(ModDependencyInstallPlan plan)
    {
        var confirmed = await _installOrchestrator.ConfirmDependencyPlanAsync(plan).ConfigureAwait(true);
        if (!confirmed)
        {
            SetDetailStatus("Install canceled.");
        }

        return confirmed;
    }

    private async Task<bool> ConfirmBlockedInstallAsync(ModDetails details, ModReleaseInfo release)
    {
        var modId = ResolveModIdentifier(details);
        var confirmed = await _installOrchestrator.ConfirmBlockedReleaseAsync(_settings, modId, release).ConfigureAwait(true);
        if (!confirmed)
        {
            SetDetailStatus("Install canceled.");
        }

        return confirmed;
    }

    private async Task RunModInstallPlanAsync(ModDependencyInstallPlan plan)
    {
        IsInstalling = true;
        _activeInstalls++;
        try
        {
            var result = await _installOrchestrator.InstallPlanAsync(
                ResolveDataPath(),
                plan,
                step => step.Depth == 0
                    ? (SelectedDetails?.Name ?? step.ModId)
                    : step.ModId).ConfigureAwait(true);

            if (!result.Success)
            {
                SetDetailStatus(result.Message ?? "Install failed.", true);
            }

            await RefreshInstalledAsync().ConfigureAwait(true);
        }
        finally
        {
            _activeInstalls = Math.Max(0, _activeInstalls - 1);
            IsInstalling = _activeInstalls > 0;
        }
    }

    private async Task RunModInstallAsync(ModDetails details, ModReleaseInfo release)
        => await RunModInstallAsync(details.Name, release, refreshInstalled: true).ConfigureAwait(true);

    private async Task RunModInstallAsync(string displayName, ModReleaseInfo release, bool refreshInstalled)
    {
        var jobId = $"mod-{release.FileId}-{Guid.NewGuid():N}";
        var session = _transfers.Begin(jobId, $"Mod {displayName}", TransferJobKind.Mod);
        try
        {
            await session.StartAsync().ConfigureAwait(true);
            IsInstalling = true;
            _activeInstalls++;
            InstallProgress = 0;
            InstallProgressLabel = $"Downloading {displayName}...";
            SetDetailStatus(InstallProgressLabel);

            var progress = new Progress<double>(value =>
            {
                InstallProgress = value;
                session.Report(value);
                InstallProgressLabel = $"Downloading {displayName}... {value:P0}";
                SetDetailStatus(InstallProgressLabel);
            });

            var result = await _modLibrary.InstallAsync(ResolveDataPath(), release, progress).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                session.Fail(result.Error ?? "Install failed.");
                SetDetailStatus(result.Error ?? "Install failed.", true);
                _logger.LogWarning("Mod install failed: {Error}", result.Error);
                return;
            }

            session.Complete($"Installed {result.Value!.FileName}");
            SetDetailStatus($"Installed {result.Value.FileName}");
            InstallProgress = 1;
            InstallProgressLabel = DetailStatus;
            if (refreshInstalled)
            {
                await RefreshInstalledAsync().ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            session.Cancel();
            SetDetailStatus("Install canceled.");
        }
        finally
        {
            _activeInstalls = Math.Max(0, _activeInstalls - 1);
            IsInstalling = _activeInstalls > 0;
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task UninstallModAsync(LocalModInfo? mod)
    {
        if (mod is null)
        {
            return;
        }

        var confirmed = await _confirmDialog.ConfirmAsync(
            "Uninstall mod",
            $"Uninstall {mod.Name}? This deletes the mod files from your data folder.",
            "Uninstall",
            "Cancel").ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        var result = await _modLibrary.UninstallAsync(mod).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            SetStatus(result.Error ?? "Uninstall failed.", true);
            return;
        }

        await RefreshInstalledAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleModAsync(LocalModInfo? mod)
    {
        if (mod is null)
        {
            return;
        }

        var enabling = !mod.IsEnabled;
        if (enabling && !string.IsNullOrWhiteSpace(mod.ModId))
        {
            var installed = _allInstalledRows.Select(r => r.Info).ToList();
            var asEnabled = new LocalModInfo
            {
                Path = mod.Path,
                FileName = mod.FileName,
                ModId = mod.ModId,
                Name = mod.Name,
                Version = mod.Version,
                IconPath = mod.IconPath,
                Dependencies = mod.Dependencies,
                IsEnabled = true,
                IsDirectory = mod.IsDirectory,
            };
            var audit = ModDependencyResolver.AuditMod(asEnabled, installed, _settings.SelectedVersion);
            var blocking = audit.Issues
                .Where(i => i.Kind is ModDependencyIssueKind.Missing
                    or ModDependencyIssueKind.Disabled
                    or ModDependencyIssueKind.Outdated
                    or ModDependencyIssueKind.BuiltinVersionMismatch)
                .ToList();
            if (blocking.Count > 0)
            {
                var summary = string.Join(
                    Environment.NewLine,
                    blocking.Take(8).Select(i => $"- {i.RequiredModId}: {i.Kind}"));
                var proceed = await _confirmDialog.ConfirmAsync(
                    "Missing dependencies",
                    $"This mod has dependency problems:{Environment.NewLine}{summary}{Environment.NewLine}Enable anyway?",
                    "Enable",
                    "Cancel").ConfigureAwait(true);
                if (!proceed)
                {
                    return;
                }
            }
        }

        var result = await _modLibrary.SetEnabledAsync(mod, enabling).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            SetStatus(result.Error ?? "Could not change mod state.", true);
            return;
        }

        await RefreshInstalledAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CleanDuplicatesAsync()
    {
        var confirmed = await _confirmDialog.ConfirmAsync(
            "Clean duplicate mods",
            "Keep the newest enabled release for each mod id and delete the rest from your Mods folder?",
            "Clean",
            "Cancel").ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        var result = await _modLibrary.CleanDuplicateModsAsync(ResolveDataPath()).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            SetStatus(result.Error ?? "Could not clean duplicate mods.", true);
            return;
        }

        SetStatus(result.Value == 0
            ? "No duplicate mods found."
            : $"Removed {result.Value} duplicate mod file(s).");
        await RefreshInstalledAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ImportLocalFolderAsync()
    {
        var path = await _storagePicker.PickFolderAsync("Select mod folder (contains modinfo.json)").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await ImportLocalPathAsync(path).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ImportLocalZipAsync()
    {
        var path = await _storagePicker.PickZipFileAsync("Select mod zip").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await ImportLocalPathAsync(path).ConfigureAwait(true);
    }

    private async Task ImportLocalPathAsync(string path)
    {
        var result = await _modLibrary.ImportLocalAsync(ResolveDataPath(), path).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            SetStatus(result.Error ?? "Could not import local mod.", true);
            return;
        }

        SetStatus($"Imported {result.Value!.FileName}");
        await RefreshInstalledAsync().ConfigureAwait(true);
    }
}
