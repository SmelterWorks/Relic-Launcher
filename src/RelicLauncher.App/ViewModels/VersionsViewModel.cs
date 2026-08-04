using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class VersionsViewModel : PageViewModelBase
{
    private const int PageSize = RelicDefaults.VersionBrowsePageSize;
    private readonly IGameVersionCatalog _catalog;
    private readonly IInstalledVersionStore _installedStore;
    private readonly IGameVersionInstaller _installer;
    private readonly ILauncherSettingsStore _settingsStore;
    private readonly IRuntimePlatform _platform;
    private readonly ITransferTracker _transfers;
    private readonly IConfirmDialogService _confirmDialog;
    private readonly ILogger<VersionsViewModel> _logger;
    private LauncherSettings _settings = new();
    private Action<LauncherSettings>? _onChanged;
    private readonly Dictionary<string, CancellationTokenSource> _installCts = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<VersionRowViewModel> _allRows = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private string _installProgressLabel = string.Empty;

    [ObservableProperty]
    private bool _showStable = true;

    [ObservableProperty]
    private bool _showUnstable;

    [ObservableProperty]
    private string _latestStable = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private string _pageLabel = string.Empty;

    [ObservableProperty]
    private bool _hasPreviousPage;

    [ObservableProperty]
    private bool _hasNextPage;

    [ObservableProperty]
    private bool _hasVersions;

    [ObservableProperty]
    private string _emptyMessage = "No versions to show.";

    public ObservableCollection<VersionRowViewModel> Versions { get; } = [];
    public ObservableCollection<TransferJobRowViewModel> ActiveTransfers { get; } = [];

    public VersionsViewModel(
        IGameVersionCatalog catalog,
        IInstalledVersionStore installedStore,
        IGameVersionInstaller installer,
        ILauncherSettingsStore settingsStore,
        IRuntimePlatform platform,
        ITransferTracker transfers,
        IConfirmDialogService confirmDialog,
        ILogger<VersionsViewModel> logger)
    {
        _catalog = catalog;
        _installedStore = installedStore;
        _installer = installer;
        _settingsStore = settingsStore;
        _platform = platform;
        _transfers = transfers;
        _confirmDialog = confirmDialog;
        _logger = logger;
        _transfers.Changed += (_, _) => OnTransfersChanged();
        OnTransfersChanged();
    }

    public void Bind(LauncherSettings settings, Action<LauncherSettings> onChanged, bool refresh = true)
    {
        _settings = settings;
        _onChanged = onChanged;
        if (string.IsNullOrWhiteSpace(_settings.InstallsRoot))
        {
            _settings.InstallsRoot = _platform.GetPlatformInfo().DefaultInstallsRoot;
        }

        if (refresh)
        {
            _ = RefreshAsync();
        }
    }

    partial void OnShowStableChanged(bool value) => ApplyFilter();

    partial void OnShowUnstableChanged(bool value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        Versions.Clear();

        var latest = await _catalog.GetLatestStableVersionAsync().ConfigureAwait(true);
        LatestStable = latest.IsSuccess ? latest.Value ?? string.Empty : string.Empty;

        var remote = await _catalog.GetVersionsAsync(cancellationToken: default).ConfigureAwait(true);
        if (!remote.IsSuccess)
        {
            StatusMessage = remote.Error ?? "Could not load version catalog.";
            _logger.LogWarning("Version catalog failed: {Error}", remote.Error);
            IsLoading = false;
            HasVersions = false;
            EmptyMessage = "Could not load versions.";
            return;
        }

        var installsRoot = _settings.InstallsRoot ?? _platform.GetPlatformInfo().DefaultInstallsRoot;
        var installed = await _installedStore.ListAsync(installsRoot).ConfigureAwait(true);
        var installedMap = installed.IsSuccess
            ? installed.Value!.ToDictionary(v => v.Version, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, InstalledGameVersion>(StringComparer.OrdinalIgnoreCase);

        var rows = new List<VersionRowViewModel>();
        foreach (var version in remote.Value!)
        {
            installedMap.TryGetValue(version.Version, out var local);
            rows.Add(new VersionRowViewModel(version, local, _settings.SelectedVersion, this));
        }

        _allRows = rows;
        Page = 1;
        ApplyFilter();
        IsLoading = false;
        if (string.IsNullOrWhiteSpace(StatusMessage))
        {
            StatusMessage = _catalog.LastCatalogWasStale
                ? "Showing saved catalog while offline."
                : $"Loaded {_allRows.Count} versions.";
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!HasNextPage)
        {
            return;
        }

        Page++;
        ApplyFilter(resetPage: false);
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (!HasPreviousPage)
        {
            return;
        }

        Page--;
        ApplyFilter(resetPage: false);
    }

    private void ApplyFilter(bool resetPage = true)
    {
        if (resetPage)
        {
            Page = 1;
        }

        IEnumerable<VersionRowViewModel> filtered = _allRows;
        if (!ShowStable)
        {
            filtered = filtered.Where(v => !string.Equals(v.Channel, "Stable", StringComparison.OrdinalIgnoreCase));
        }

        if (!ShowUnstable)
        {
            filtered = filtered.Where(v => !string.Equals(v.Channel, "Unstable", StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var text = SearchText.Trim();
            filtered = filtered.Where(v =>
                v.Version.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                v.PackageSummary.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        var total = list.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        if (Page > totalPages)
        {
            Page = totalPages;
        }

        Versions.Clear();
        foreach (var row in list.Skip((Page - 1) * PageSize).Take(PageSize))
        {
            Versions.Add(row);
        }

        HasVersions = Versions.Count > 0;
        if (HasVersions)
        {
            EmptyMessage = string.Empty;
        }
        else if (!ShowStable && !ShowUnstable)
        {
            EmptyMessage = "Turn on Stable and/or Unstable to list versions.";
        }
        else
        {
            EmptyMessage = "No versions matched your filters.";
        }

        PageLabel = total == 0 ? "No results" : $"Page {Page} of {totalPages} ({total})";
        HasPreviousPage = Page > 1;
        HasNextPage = Page * PageSize < total;
    }

    [RelayCommand]
    private void CancelAllInstalls()
    {
        foreach (var cts in _installCts.Values.ToList())
        {
            cts.Cancel();
        }
    }

    internal async Task InstallAsync(GameVersionInfo version)
    {
        var installsRoot = _settings.InstallsRoot ?? _platform.GetPlatformInfo().DefaultInstallsRoot;
        Directory.CreateDirectory(installsRoot);
        _settings.InstallsRoot = installsRoot;

        if (_installCts.TryGetValue(version.Version, out var existing))
        {
            existing.Cancel();
        }

        var cts = new CancellationTokenSource();
        _installCts[version.Version] = cts;

        var session = _transfers.Begin(
            $"version-{version.Version}-{Guid.NewGuid():N}",
            $"Version {version.Version}",
            TransferJobKind.Version);

        try
        {
            await RunVersionInstallAsync(version, installsRoot, session, cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            session.Cancel();
            StatusMessage = $"Install of {version.Version} canceled.";
        }
        finally
        {
            _installCts.Remove(version.Version);
            IsInstalling = _installCts.Count > 0;
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task RunVersionInstallAsync(
        GameVersionInfo version,
        string installsRoot,
        ITransferSession session,
        CancellationToken cancellationToken)
    {
        await session.StartAsync(cancellationToken).ConfigureAwait(true);
        IsInstalling = true;
        InstallProgress = 0;
        InstallProgressLabel = $"Downloading {version.Version}...";
        StatusMessage = string.Empty;

        var progress = new Progress<double>(value =>
        {
            InstallProgress = value;
            session.Report(value);
            InstallProgressLabel = value >= 0.9
                ? $"Installing {version.Version}..."
                : $"Downloading {version.Version}... {value:P0}";
        });

        var result = await _installer.InstallAsync(new VersionInstallRequest
        {
            InstallsRoot = installsRoot,
            Version = version,
            Progress = progress,
        }, cancellationToken).ConfigureAwait(true);

        if (!result.IsSuccess)
        {
            session.Fail(result.Error ?? "Install failed.");
            StatusMessage = result.Error ?? "Install failed.";
            _logger.LogWarning("Install failed: {Error}", result.Error);
            return;
        }

        session.Complete($"Installed {version.Version}");
        _settings.SelectedVersion = version.Version;
        await PersistSettingsAsync().ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        StatusMessage = $"Installed {version.Version}.";
        InstallProgress = 1;
        InstallProgressLabel = StatusMessage;
    }

    internal async Task UninstallAsync(string version)
    {
        var confirmed = await _confirmDialog.ConfirmAsync(
            "Uninstall version",
            $"Uninstall Vintage Story {version}? This removes the managed install folder.",
            "Uninstall",
            "Cancel").ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        var installsRoot = _settings.InstallsRoot ?? _platform.GetPlatformInfo().DefaultInstallsRoot;
        var result = await _installer.UninstallAsync(installsRoot, version).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            StatusMessage = result.Error ?? "Uninstall failed.";
            return;
        }

        if (string.Equals(_settings.SelectedVersion, version, StringComparison.OrdinalIgnoreCase))
        {
            _settings.SelectedVersion = null;
            await PersistSettingsAsync().ConfigureAwait(true);
        }

        await RefreshAsync().ConfigureAwait(true);
        StatusMessage = $"Uninstalled {version}.";
    }

    internal async Task SetActiveAsync(string version)
    {
        _settings.SelectedVersion = version;
        await PersistSettingsAsync().ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        StatusMessage = $"Active version set to {version}.";
    }

    private async Task PersistSettingsAsync()
    {
        var save = await _settingsStore.SaveAsync(_settings).ConfigureAwait(true);
        if (save.IsSuccess)
        {
            _onChanged?.Invoke(_settings);
        }
    }

    private void OnTransfersChanged()
    {
        void Apply()
        {
            ActiveTransfers.Clear();
            foreach (var job in _transfers.GetJobs().Where(j =>
                         j.Kind == TransferJobKind.Version &&
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
