using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class BackupViewModel : PageViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly IInstalledVersionStore _installedStore;
    private readonly IRuntimePlatform _platform;
    private readonly IStoragePickerService _storagePicker;
    private readonly IConfirmDialogService _confirmDialog;
    private readonly IFileExplorerService _fileExplorer;
    private readonly ITransferTracker _transfers;
    private readonly ILogger<BackupViewModel> _logger;
    private LauncherSettings _settings = new();

    [ObservableProperty]
    private bool _includeMods = true;

    [ObservableProperty]
    private bool _includeWorlds = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressLabel = string.Empty;

    [ObservableProperty]
    private bool _hasInstalledVersions;

    public ObservableCollection<BackupVersionRowViewModel> InstalledVersions { get; } = [];
    public ObservableCollection<TransferJobRowViewModel> ActiveTransfers { get; } = [];

    public BackupViewModel(
        IBackupService backupService,
        IInstalledVersionStore installedStore,
        IRuntimePlatform platform,
        IStoragePickerService storagePicker,
        IConfirmDialogService confirmDialog,
        IFileExplorerService fileExplorer,
        ITransferTracker transfers,
        ILogger<BackupViewModel> logger)
    {
        _backupService = backupService;
        _installedStore = installedStore;
        _platform = platform;
        _storagePicker = storagePicker;
        _confirmDialog = confirmDialog;
        _fileExplorer = fileExplorer;
        _transfers = transfers;
        _logger = logger;
        _transfers.Changed += (_, _) => OnTransfersChanged();
        OnTransfersChanged();
    }

    public void Bind(LauncherSettings settings, bool refresh = true)
    {
        _settings = settings;
        if (refresh)
        {
            _ = RefreshInstalledVersionsAsync();
        }
    }

    private async Task RefreshInstalledVersionsAsync()
    {
        var installsRoot = _settings.InstallsRoot ?? _platform.GetPlatformInfo().DefaultInstallsRoot;
        var installed = await _installedStore.ListAsync(installsRoot).ConfigureAwait(true);
        var selected = InstalledVersions.Where(v => v.IsSelected).Select(v => v.Version).ToHashSet(StringComparer.OrdinalIgnoreCase);
        InstalledVersions.Clear();
        if (installed.IsSuccess)
        {
            foreach (var version in installed.Value!.OrderByDescending(v => v.InstalledAt))
            {
                InstalledVersions.Add(new BackupVersionRowViewModel(version.Version) { IsSelected = selected.Contains(version.Version) });
            }
        }

        HasInstalledVersions = InstalledVersions.Count > 0;
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        var selectedVersions = InstalledVersions.Where(v => v.IsSelected).Select(v => v.Version).ToList();
        if (!IncludeMods && !IncludeWorlds && selectedVersions.Count == 0)
        {
            SetStatus("Select at least one thing to back up.", true);
            return;
        }

        var suggested = $"relic-backup-{DateTimeOffset.Now:yyyyMMdd-HHmm}.zip";
        var destination = await _storagePicker.SaveZipFileAsync(suggested, "Save backup as").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        await RunCreateAsync(destination, selectedVersions).ConfigureAwait(true);
    }

    private async Task RunCreateAsync(string destination, List<string> selectedVersions)
    {
        var session = _transfers.Begin($"backup-{Guid.NewGuid():N}", "Creating backup", TransferJobKind.Backup);
        IsBusy = true;
        SetStatus(string.Empty);
        try
        {
            await session.StartAsync().ConfigureAwait(true);
            Progress = 0;
            ProgressLabel = "Creating backup...";
            var progress = new Progress<double>(value =>
            {
                Progress = value;
                session.Report(value);
            });

            var dataPath = _settings.DataPath ?? _platform.GetPlatformInfo().DefaultDataPath;
            var installsRoot = _settings.InstallsRoot ?? _platform.GetPlatformInfo().DefaultInstallsRoot;
            var result = await _backupService.CreateAsync(new BackupRequest
            {
                DestinationZipPath = destination,
                DataPath = dataPath,
                InstallsRoot = installsRoot,
                IncludeMods = IncludeMods,
                IncludeWorlds = IncludeWorlds,
                VersionsToInclude = selectedVersions,
                Progress = progress,
            }).ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                session.Fail(result.Error ?? "Backup failed.");
                SetStatus(result.Error ?? "Backup failed.", true);
                _logger.LogWarning("Backup creation failed: {Error}", result.Error);
                return;
            }

            session.Complete("Backup created");
            SetStatus($"Created backup with {result.Value!.FileCount} file(s), {FormatBytes(result.Value.TotalBytes)} at {destination}.");
            var folder = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                _fileExplorer.OpenFolder(folder);
            }
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        var source = await _storagePicker.PickZipFileAsync("Select a Relic backup to restore").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        var manifest = await _backupService.ReadManifestAsync(source).ConfigureAwait(true);
        if (!manifest.IsSuccess)
        {
            SetStatus(manifest.Error ?? "Could not read backup.", true);
            return;
        }

        var summary = DescribeManifest(manifest.Value!);
        var confirmed = await _confirmDialog.ConfirmAsync(
            "Restore backup",
            $"This will overwrite matching files.\n\n{summary}",
            "Restore",
            "Cancel").ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        await RunRestoreAsync(source).ConfigureAwait(true);
    }

    private async Task RunRestoreAsync(string source)
    {
        var session = _transfers.Begin($"restore-{Guid.NewGuid():N}", "Restoring backup", TransferJobKind.Backup);
        IsBusy = true;
        SetStatus(string.Empty);
        try
        {
            await session.StartAsync().ConfigureAwait(true);
            Progress = 0;
            ProgressLabel = "Restoring backup...";
            var progress = new Progress<double>(value =>
            {
                Progress = value;
                session.Report(value);
            });

            var dataPath = _settings.DataPath ?? _platform.GetPlatformInfo().DefaultDataPath;
            var installsRoot = _settings.InstallsRoot ?? _platform.GetPlatformInfo().DefaultInstallsRoot;
            var result = await _backupService.RestoreAsync(new BackupRestoreRequest
            {
                SourceZipPath = source,
                DataPath = dataPath,
                InstallsRoot = installsRoot,
                Progress = progress,
            }).ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                session.Fail(result.Error ?? "Restore failed.");
                SetStatus(result.Error ?? "Restore failed.", true);
                _logger.LogWarning("Backup restore failed: {Error}", result.Error);
                return;
            }

            session.Complete("Backup restored");
            SetStatus($"Restored {result.Value!.FileCount} file(s).");
            await RefreshInstalledVersionsAsync().ConfigureAwait(true);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
            IsBusy = false;
        }
    }

    private static string DescribeManifest(BackupManifest manifest)
    {
        var parts = new List<string>();
        if (manifest.IncludesMods)
        {
            parts.Add("Mods");
        }

        if (manifest.IncludesWorlds)
        {
            parts.Add("Worlds");
        }

        if (manifest.Versions.Count > 0)
        {
            parts.Add($"Versions: {string.Join(", ", manifest.Versions)}");
        }

        var contents = parts.Count > 0 ? string.Join(", ", parts) : "Nothing";
        return $"Created {manifest.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm} with Relic {manifest.RelicVersion}.\nContains: {contents}";
    }

    private static string FormatBytes(long bytes)
    {
        const double mib = 1024d * 1024d;
        const double gib = mib * 1024d;
        return bytes >= gib ? $"{bytes / gib:0.#} GiB" : $"{bytes / mib:0.#} MiB";
    }

    private void OnTransfersChanged()
    {
        void Apply()
        {
            ActiveTransfers.Clear();
            foreach (var job in _transfers.GetJobs().Where(j =>
                         j.Kind == TransferJobKind.Backup &&
                         (j.State == TransferJobState.Queued || j.State == TransferJobState.Running)))
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
