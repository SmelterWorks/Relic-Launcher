using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Versions;
using RelicLauncher.Infrastructure.Server;

namespace RelicLauncher.App.ViewModels;

public partial class HostingViewModel : PageViewModelBase
{
    private readonly IRuntimePlatform _platform;
    private readonly IFileExplorerService _fileExplorer;
    private readonly IStoragePickerService _storagePicker;
    private readonly IUrlLauncher _urlLauncher;
    private readonly ILauncherSettingsStore _settingsStore;
    private readonly IGameVersionCatalog _catalog;
    private readonly IInstalledServerStore _installedStore;
    private readonly IGameServerInstaller _installer;
    private readonly IGameServerHost _serverHost;
    private readonly IAccountAuthService _accountAuth;
    private readonly IDotNetRuntimeProvisioner _runtimeProvisioner;
    private readonly ITransferTracker _transfers;
    private readonly ISmelterWorksHostingFeedService _hostingFeed;
    private readonly IConfirmDialogService _confirmDialog;
    private readonly ILogger<HostingViewModel> _logger;
    private LauncherSettings _settings = new();
    private Action<LauncherSettings>? _onChanged;
    private CancellationTokenSource? _installCts;

    public HostingViewModel(
        IRuntimePlatform platform,
        IFileExplorerService fileExplorer,
        IStoragePickerService storagePicker,
        IUrlLauncher urlLauncher,
        ILauncherSettingsStore settingsStore,
        IGameVersionCatalog catalog,
        IInstalledServerStore installedStore,
        IGameServerInstaller installer,
        IGameServerHost serverHost,
        IAccountAuthService accountAuth,
        IDotNetRuntimeProvisioner runtimeProvisioner,
        ITransferTracker transfers,
        ISmelterWorksHostingFeedService hostingFeed,
        IConfirmDialogService confirmDialog,
        ILogger<HostingViewModel> logger)
    {
        _platform = platform;
        _fileExplorer = fileExplorer;
        _storagePicker = storagePicker;
        _urlLauncher = urlLauncher;
        _settingsStore = settingsStore;
        _catalog = catalog;
        _installedStore = installedStore;
        _installer = installer;
        _serverHost = serverHost;
        _accountAuth = accountAuth;
        _runtimeProvisioner = runtimeProvisioner;
        _transfers = transfers;
        _hostingFeed = hostingFeed;
        _confirmDialog = confirmDialog;
        _logger = logger;

        ServerInstallFolder = new FolderPathRowViewModel(fileExplorer);
        ServerDataFolder = new FolderPathRowViewModel(fileExplorer);
        IsLocalHostingSupported = _platform.GetPlatformInfo().Os is HostOs.Windows or HostOs.Linux;

        _serverHost.StateChanged += OnServerHostStateChanged;
        _serverHost.OutputChanged += OnServerHostOutputChanged;
        CloudPlans.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ShowCloudPlanCards));
            OnPropertyChanged(nameof(ShowCloudPlansEmpty));
            NotifyCloudPlansLayoutRefresh();
        };
        SetServerState(_serverHost.State);
        RefreshConsoleText(_serverHost.OutputLines);
    }

    public bool IsLocalHostingSupported { get; }

    public FolderPathRowViewModel ServerInstallFolder { get; }

    public FolderPathRowViewModel ServerDataFolder { get; }

    public ObservableCollection<string> InstalledServerVersions { get; } = [];

    public ObservableCollection<string> CatalogServerVersions { get; } = [];

    public ObservableCollection<string> ListeningEndpoints { get; } = [];

    public ObservableCollection<HostingPlanCardViewModel> CloudPlans { get; } = [];

    [ObservableProperty]
    private HostingSection _section;

    [ObservableProperty]
    private bool _isLoadingCloudPlans;

    [ObservableProperty]
    private string? _selectedInstalledVersion;

    [ObservableProperty]
    private string? _selectedCatalogVersion;

    [ObservableProperty]
    private string _serverStateLabel = "Stopped";

    [ObservableProperty]
    private string _consoleText = string.Empty;

    [ObservableProperty]
    private string _commandText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressLabel = string.Empty;

    [ObservableProperty]
    private bool _showEntitlementNote;

    [ObservableProperty]
    private string _entitlementNote = string.Empty;

    public bool CanStart => IsLocalHostingSupported
        && !IsBusy
        && ServerState is ServerProcessState.Stopped
        && !string.IsNullOrWhiteSpace(SelectedInstalledVersion);

    public bool CanStop => IsLocalHostingSupported
        && !IsBusy
        && ServerState is ServerProcessState.Running or ServerProcessState.Starting;

    public bool CanRestart => IsLocalHostingSupported
        && !IsBusy
        && ServerState is ServerProcessState.Running
        && !string.IsNullOrWhiteSpace(SelectedInstalledVersion);

    public bool CanSendCommand => IsLocalHostingSupported && ServerState is ServerProcessState.Running;

    public bool HasInstalledVersions => InstalledServerVersions.Count > 0;

    public bool HasListeningEndpoints => ListeningEndpoints.Count > 0;

    public bool IsLocalSection => Section == HostingSection.Local;

    public bool IsCloudSection => Section == HostingSection.Cloud;

    public bool ShowCloudPlanCards => !IsLoadingCloudPlans && CloudPlans.Count > 0;

    public bool ShowCloudPlansEmpty => !IsLoadingCloudPlans && CloudPlans.Count == 0;

    public string CloudPlansEmptyMessage =>
        StatusIsError && !string.IsNullOrWhiteSpace(StatusMessage)
            ? StatusMessage
            : "Could not load SmelterWorks hosting plans.";

    public bool ShowListeningEndpoints => ServerState is ServerProcessState.Running && HasListeningEndpoints;

    public ServerProcessState ServerState { get; private set; } = ServerProcessState.Stopped;

    partial void OnSectionChanged(HostingSection value)
    {
        OnPropertyChanged(nameof(IsLocalSection));
        OnPropertyChanged(nameof(IsCloudSection));
        if (value == HostingSection.Cloud)
        {
            RequestCloudPlansLoadIfNeeded();
        }
    }

    internal event EventHandler? CloudPlansLayoutRefreshRequested;

    internal void RequestCloudPlansLoadIfNeeded()
    {
        if (!IsCloudSection || IsLoadingCloudPlans)
        {
            return;
        }

        if (CloudPlans.Count > 0)
        {
            NotifyCloudPlansLayoutRefresh();
            return;
        }

        _ = LoadCloudPlansAsync();
    }

    private void NotifyCloudPlansLayoutRefresh()
        => CloudPlansLayoutRefreshRequested?.Invoke(this, EventArgs.Empty);

    public void Bind(LauncherSettings settings, Action<LauncherSettings> onChanged, bool refresh = true)
    {
        _settings = settings;
        _onChanged = onChanged;

        var platform = _platform.GetPlatformInfo();
        if (string.IsNullOrWhiteSpace(_settings.InstallsRoot))
        {
            _settings.InstallsRoot = platform.DefaultInstallsRoot;
        }

        if (string.IsNullOrWhiteSpace(_settings.ServerDataPath))
        {
            _settings.ServerDataPath = platform.DefaultServerDataPath;
        }

        SelectedInstalledVersion = _settings.SelectedServerVersion;
        Section = IsLocalHostingSupported ? HostingSection.Local : HostingSection.Cloud;
        UpdateFolderRows();
        RefreshListeningEndpoints();

        if (refresh && IsLocalHostingSupported)
        {
            _ = RefreshAsync();
        }

        if (Section == HostingSection.Cloud)
        {
            RequestCloudPlansLoadIfNeeded();
        }
    }

    [RelayCommand]
    private void ShowLocalSection()
    {
        if (!IsLocalHostingSupported)
        {
            return;
        }

        Section = HostingSection.Local;
    }

    [RelayCommand]
    private void ShowCloudSection() => Section = HostingSection.Cloud;

    partial void OnSelectedInstalledVersionChanged(string? value)
    {
        _settings.SelectedServerVersion = value;
        _onChanged?.Invoke(_settings);
        UpdateFolderRows();
        NotifyCommandState();
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommandState();

    partial void OnCommandTextChanged(string value) => OnPropertyChanged(nameof(CanSendCommand));

    private void UpdateFolderRows()
    {
        var installsRoot = ResolveInstallsRoot();
        var version = SelectedInstalledVersion ?? _settings.SelectedServerVersion ?? string.Empty;
        var installPath = string.IsNullOrWhiteSpace(version)
            ? Path.Combine(installsRoot, "servers")
            : Path.Combine(installsRoot, "servers", version.Trim());

        ServerInstallFolder.Bind("Server install folder", installPath);
        ServerDataFolder.Bind("Server data folder", ResolveServerDataPath());
    }

    private string ResolveInstallsRoot()
        => _settings.InstallsRoot ?? _platform.GetPlatformInfo().DefaultInstallsRoot;

    private string ResolveServerDataPath()
        => _settings.ServerDataPath ?? _platform.GetPlatformInfo().DefaultServerDataPath;

    private async Task PersistSettingsAsync()
    {
        _onChanged?.Invoke(_settings);
        await _settingsStore.SaveAsync(_settings).ConfigureAwait(true);
    }

    private void SetServerState(ServerProcessState state)
    {
        ServerState = state;
        ServerStateLabel = state switch
        {
            ServerProcessState.Starting => "Starting",
            ServerProcessState.Running => "Running",
            ServerProcessState.Stopping => "Stopping",
            _ => "Stopped",
        };
        NotifyCommandState();
        OnPropertyChanged(nameof(ShowListeningEndpoints));
        RefreshListeningEndpoints();
    }

    private void RefreshListeningEndpoints()
    {
        ListeningEndpoints.Clear();
        if (ServerState is not ServerProcessState.Running and not ServerProcessState.Starting)
        {
            OnPropertyChanged(nameof(HasListeningEndpoints));
            OnPropertyChanged(nameof(ShowListeningEndpoints));
            return;
        }

        foreach (var endpoint in ServerListenEndpointResolver.Resolve(ResolveServerDataPath()))
        {
            ListeningEndpoints.Add(endpoint);
        }

        OnPropertyChanged(nameof(HasListeningEndpoints));
        OnPropertyChanged(nameof(ShowListeningEndpoints));
    }

    private void RefreshConsoleText(IReadOnlyList<string> lines)
    {
        ConsoleText = string.Join(Environment.NewLine, lines);
    }

    private void NotifyCommandState()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanRestart));
        OnPropertyChanged(nameof(CanSendCommand));
        OnPropertyChanged(nameof(HasInstalledVersions));
        OnPropertyChanged(nameof(HasUpgradeAvailable));
        OnPropertyChanged(nameof(CanUninstallSelected));
        OnPropertyChanged(nameof(CanUpgradeToLatest));
        OnPropertyChanged(nameof(UpgradeTooltip));
        OnPropertyChanged(nameof(HasInstallableServerVersions));
        OnPropertyChanged(nameof(ShowCatalogVersionPicker));
        OnPropertyChanged(nameof(CanInstallLatestServer));
        OnPropertyChanged(nameof(InstallLatestLabel));
    }

    private void OnServerHostStateChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SetServerState(_serverHost.State);
            RefreshListeningEndpoints();
        });
    }

    private void OnServerHostOutputChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RefreshConsoleText(_serverHost.OutputLines);
            if (ServerState is ServerProcessState.Running or ServerProcessState.Starting)
            {
                RefreshListeningEndpoints();
            }
        });
    }
}
