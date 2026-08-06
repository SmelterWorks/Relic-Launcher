using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;

namespace RelicLauncher.App.ViewModels;

public partial class ModpackPanelViewModel : ObservableObject
{
    private readonly IModpackService _modpackService;
    private readonly IModLibraryService _modLibrary;
    private readonly IModReleaseResolver _releaseResolver;
    private readonly ModInstallOrchestrator _installOrchestrator;
    private readonly IRuntimePlatform _platform;
    private readonly IStoragePickerService _storagePicker;
    private readonly IConfirmDialogService _confirmDialog;
    private readonly ITransferTracker _transfers;
    private readonly ILogger<ModpackPanelViewModel> _logger;
    private LauncherSettings _settings = new();

    [ObservableProperty]
    private string _newPackName = string.Empty;

    [ObservableProperty]
    private string _newPackDescription = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _distributionNote = string.Empty;

    [ObservableProperty]
    private bool _hasSavedPacks;

    public ObservableCollection<ModpackRowViewModel> SavedPacks { get; } = [];
    public ObservableCollection<TransferJobRowViewModel> ActiveTransfers { get; } = [];

    public ModpackPanelViewModel(
        IModpackService modpackService,
        IModLibraryService modLibrary,
        IModReleaseResolver releaseResolver,
        ModInstallOrchestrator installOrchestrator,
        IRuntimePlatform platform,
        IStoragePickerService storagePicker,
        IConfirmDialogService confirmDialog,
        ITransferTracker transfers,
        ILogger<ModpackPanelViewModel> logger)
    {
        _modpackService = modpackService;
        _modLibrary = modLibrary;
        _releaseResolver = releaseResolver;
        _installOrchestrator = installOrchestrator;
        _platform = platform;
        _storagePicker = storagePicker;
        _confirmDialog = confirmDialog;
        _transfers = transfers;
        _logger = logger;
        _transfers.Changed += (_, _) => OnTransfersChanged();
        OnTransfersChanged();
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public void Bind(LauncherSettings settings, bool refresh = true)
    {
        _settings = settings;
        if (refresh)
        {
            _ = RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var listed = await _modpackService.ListLocalAsync().ConfigureAwait(true);
        SavedPacks.Clear();
        if (listed.IsSuccess)
        {
            foreach (var pack in listed.Value!)
            {
                SavedPacks.Add(new ModpackRowViewModel(pack));
            }
        }

        HasSavedPacks = SavedPacks.Count > 0;
        if (!listed.IsSuccess)
        {
            StatusMessage = listed.Error ?? "Could not list modpacks.";
        }
    }

    [RelayCommand]
    private async Task SaveFromInstalledAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPackName))
        {
            StatusMessage = "Enter a modpack name.";
            return;
        }

        var gameVersion = _settings.SelectedVersion;
        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            StatusMessage = "Set an active game version first.";
            return;
        }

        var dataPath = ResolveDataPath();
        var installed = await _modLibrary.ListInstalledAsync(dataPath).ConfigureAwait(true);
        if (!installed.IsSuccess)
        {
            StatusMessage = installed.Error ?? "Could not list installed mods.";
            return;
        }

        var mods = installed.Value!
            .Where(m => m.IsEnabled && !BuiltinModIds.IsBuiltin(m.ModId))
            .ToList();
        if (mods.Count == 0)
        {
            StatusMessage = "No enabled mods to save.";
            return;
        }

        UpdateDistributionNote(mods);
        IsBusy = true;
        try
        {
            var result = await _modpackService.SaveLocalAsync(new ModpackSaveRequest
            {
                DataPath = dataPath,
                GameVersion = gameVersion,
                Name = NewPackName.Trim(),
                Description = NewPackDescription.Trim(),
                Mods = mods,
            }).ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                StatusMessage = result.Error ?? "Could not save modpack.";
                return;
            }

            StatusMessage = $"Saved modpack {result.Value!.Name}.";
            NewPackName = string.Empty;
            NewPackDescription = string.Empty;
            DistributionNote = string.Empty;
            await RefreshAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportAndApplyReplaceAsync()
        => await ImportAndApplyAsync(ModpackApplyMode.Replace).ConfigureAwait(true);

    [RelayCommand]
    private async Task ImportAndApplyMergeAsync()
        => await ImportAndApplyAsync(ModpackApplyMode.Merge).ConfigureAwait(true);

    [RelayCommand]
    private async Task ApplyReplaceAsync(ModpackRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await ApplyPackAsync(row.Summary.DirectoryPath, ModpackApplyMode.Replace).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ApplyMergeAsync(ModpackRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await ApplyPackAsync(row.Summary.DirectoryPath, ModpackApplyMode.Merge).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ExportAsync(ModpackRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var path = await _storagePicker.SaveModpackFileAsync($"{SanitizeFileName(row.Name)}.relicmodpack", "Export modpack").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _modpackService.ExportSavedAsync(row.Summary.DirectoryPath, path).ConfigureAwait(true);
            StatusMessage = result.IsSuccess
                ? $"Exported {row.Name}."
                : result.Error ?? "Export failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(ModpackRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var confirmed = await _confirmDialog.ConfirmAsync(
            "Delete modpack",
            $"Delete saved modpack {row.Name}?",
            "Delete",
            "Cancel").ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        var result = await _modpackService.DeleteLocalAsync(row.Summary.Id).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            StatusMessage = result.Error ?? "Delete failed.";
            return;
        }

        StatusMessage = $"Deleted {row.Name}.";
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task ImportAndApplyAsync(ModpackApplyMode mode)
    {
        var path = await _storagePicker.PickModpackFileAsync("Import modpack").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await ApplyPackAsync(path, mode).ConfigureAwait(true);
    }

    private async Task ApplyPackAsync(string packPath, ModpackApplyMode mode)
    {
        var manifestResult = await _modpackService.ReadManifestAsync(packPath).ConfigureAwait(true);
        if (!manifestResult.IsSuccess)
        {
            StatusMessage = manifestResult.Error ?? "Could not read modpack.";
            return;
        }

        var manifest = manifestResult.Value!;
        if (!await ConfirmVersionMismatchAsync(manifest).ConfigureAwait(true))
        {
            return;
        }

        if (!await ConfirmBlockedModsAsync(manifest).ConfigureAwait(true))
        {
            StatusMessage = "Apply canceled.";
            return;
        }

        var dataPath = ResolveDataPath();
        var applyRequest = new ModpackApplyRequest
        {
            DataPath = dataPath,
            Manifest = manifest,
            ZipPath = packPath,
            Mode = mode,
        };

        var diff = await _modpackService.ComputeApplyDiffAsync(applyRequest).ConfigureAwait(true);
        if (!diff.IsSuccess)
        {
            StatusMessage = diff.Error ?? "Could not compute modpack changes.";
            return;
        }

        if (!await ConfirmApplyDiffAsync(manifest, mode, diff.Value!).ConfigureAwait(true))
        {
            StatusMessage = "Apply canceled.";
            return;
        }

        await RunApplyAsync(dataPath, manifest, packPath, mode).ConfigureAwait(true);
    }

    private async Task RunApplyAsync(string dataPath, ModpackManifest manifest, string packPath, ModpackApplyMode mode)
    {
        var jobId = $"modpack-{Guid.NewGuid():N}";
        var session = _transfers.Begin(jobId, $"Modpack {manifest.Name}", TransferJobKind.Modpack);
        IsBusy = true;
        try
        {
            await session.StartAsync().ConfigureAwait(true);
            var progress = new Progress<double>(session.Report);
            var result = await _modpackService.ApplyAsync(new ModpackApplyRequest
            {
                DataPath = dataPath,
                Manifest = manifest,
                ZipPath = packPath,
                Mode = mode,
                Progress = progress,
            }).ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                session.Fail(result.Error ?? "Modpack apply failed.");
                StatusMessage = result.Error ?? "Modpack apply failed.";
                return;
            }

            var summary = result.Value!;
            session.Complete($"Applied {manifest.Name}");
            StatusMessage = $"Applied {manifest.Name}: {summary.InstalledCount} installed, {summary.UpdatedCount} updated, {summary.RemovedCount} removed, {summary.SkippedCount} skipped.";
            if (summary.FailedCount > 0)
            {
                StatusMessage += $" {summary.FailedCount} failed.";
            }
        }
        catch (OperationCanceledException)
        {
            session.Cancel();
            StatusMessage = "Apply canceled.";
        }
        finally
        {
            IsBusy = false;
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<bool> ConfirmVersionMismatchAsync(ModpackManifest manifest)
    {
        var active = _settings.SelectedVersion;
        if (string.IsNullOrWhiteSpace(active)
            || string.Equals(active, manifest.GameVersion, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return await _confirmDialog.ConfirmAsync(
            "Game version mismatch",
            $"This modpack targets game version {manifest.GameVersion}. Your active version is {active}. Apply anyway?",
            "Apply",
            "Cancel").ConfigureAwait(false);
    }

    private async Task<bool> ConfirmBlockedModsAsync(ModpackManifest manifest)
    {
        if (manifest.Distribution == ModpackDistribution.Offline || !_settings.WarnOnBlockedMods)
        {
            return true;
        }

        foreach (var mod in manifest.Mods)
        {
            if (BuiltinModIds.IsBuiltin(mod.ModId))
            {
                continue;
            }

            var release = new ModReleaseInfo
            {
                FileId = mod.FileId,
                ModVersion = mod.ModVersion ?? string.Empty,
                DownloadUrl = "https://mods.vintagestory.at/download",
            };
            if (!await _installOrchestrator.ConfirmBlockedReleaseAsync(_settings, mod.ModId, release).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> ConfirmApplyDiffAsync(ModpackManifest manifest, ModpackApplyMode mode, ModpackApplyDiff diff)
    {
        var lines = new List<string>
        {
            $"Apply modpack {manifest.Name}?",
            mode == ModpackApplyMode.Replace
                ? "Replace mode removes mods that are not in the pack."
                : "Merge mode keeps mods that are not in the pack.",
        };

        foreach (var entry in diff.Entries)
        {
            switch (entry.Kind)
            {
                case ModpackApplyDiffKind.Add:
                    lines.Add($"+ {entry.ModId} {entry.PackVersion ?? ""}".Trim());
                    break;
                case ModpackApplyDiffKind.Update:
                    lines.Add($"~ {entry.ModId} {entry.CurrentVersion} -> {entry.PackVersion}");
                    break;
                case ModpackApplyDiffKind.Remove:
                    lines.Add($"- {entry.ModId} {entry.CurrentVersion ?? ""}".Trim());
                    break;
                case ModpackApplyDiffKind.Skip:
                    lines.Add($"= {entry.ModId} {entry.CurrentVersion ?? entry.PackVersion ?? ""}".Trim());
                    break;
            }
        }

        return await _confirmDialog.ConfirmAsync(
            "Apply modpack",
            string.Join(Environment.NewLine, lines),
            "Apply",
            "Cancel").ConfigureAwait(false);
    }

    private void UpdateDistributionNote(IReadOnlyList<LocalModInfo> mods)
    {
        var hasLocal = mods.Any(m => m.IsDirectory || !m.FileName.StartsWith("mod_", StringComparison.OrdinalIgnoreCase));
        DistributionNote = hasLocal
            ? "Local mod detected. This pack will be saved as offline with embedded mod files."
            : "All mods trace to ModDB. This pack will be saved as online.";
    }

    private string ResolveDataPath()
        => string.IsNullOrWhiteSpace(_settings.DataPath)
            ? _platform.GetPlatformInfo().DefaultDataPath
            : _settings.DataPath;

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "modpack" : value.Trim();
    }

    private void OnTransfersChanged()
    {
        void Apply()
        {
            ActiveTransfers.Clear();
            foreach (var job in _transfers.GetJobs().Where(j =>
                         j.Kind == TransferJobKind.Modpack &&
                         j.State is TransferJobState.Queued or TransferJobState.Running))
            {
                ActiveTransfers.Add(new TransferJobRowViewModel(job));
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Dispatcher.UIThread.Post(Apply);
        }
    }
}
