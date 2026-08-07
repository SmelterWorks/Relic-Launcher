using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class ServersViewModel : PageViewModelBase
{
    private readonly IMasterServerClient _masterServerClient;
    private readonly IGameLaunchService _launchService;
    private readonly IAccountAuthService _accountAuth;
    private readonly IFavoriteServersStore _favorites;
    private readonly IRecentServersStore _recents;
    private readonly IGameServerHost _serverHost;
    private readonly ILanServerScanner _lanScanner;
    private readonly IRuntimePlatform _platform;
    private readonly ITransferTracker _transfers;
    private readonly IUrlLauncher _urlLauncher;
    private readonly ILogger<ServersViewModel> _logger;
    private LauncherSettings _settings = new();
    private Action<string>? _navigateToSection;
    private CancellationTokenSource? _filterCts;
    private int _filterGeneration;
    private readonly Lock _filterGate = new();
    private IReadOnlyList<PublicServerSummary> _allServers = [];
    private HashSet<string> _favoriteAddresses = new(StringComparer.OrdinalIgnoreCase);
    private bool _ready;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasBrowseResults;

    [ObservableProperty]
    private bool _showEmptyBrowse;

    [ObservableProperty]
    private bool _showCatalogError;

    [ObservableProperty]
    private string _emptyBrowseMessage = string.Empty;

    [ObservableProperty]
    private string _catalogErrorMessage = string.Empty;

    [ObservableProperty]
    private bool _filterHasPlayers;

    [ObservableProperty]
    private bool _filterNoPassword;

    [ObservableProperty]
    private bool _filterNotWhitelisted;

    [ObservableProperty]
    private bool _filterVanilla;

    [ObservableProperty]
    private bool _filterFavoritesOnly;

    [ObservableProperty]
    private ServerSortOption? _selectedSortOption;

    [ObservableProperty]
    private ServerVersionFilterOption? _selectedVersionFilter;

    [ObservableProperty]
    private ServerRowViewModel? _selectedBrowseServer;

    [ObservableProperty]
    private string _selectedDetailDescription = string.Empty;

    [ObservableProperty]
    private string _selectedDetailAddress = string.Empty;

    [ObservableProperty]
    private string _selectedDetailMeta = string.Empty;

    [ObservableProperty]
    private string _versionMismatchWarning = string.Empty;

    [ObservableProperty]
    private bool _canJoin;

    [ObservableProperty]
    private bool _isJoining;

    [ObservableProperty]
    private bool _isSignedIn;

    [ObservableProperty]
    private bool _hasInstalledVersion;

    [ObservableProperty]
    private string _directAddress = string.Empty;

    [ObservableProperty]
    private string _directPassword = string.Empty;

    [ObservableProperty]
    private string _directValidationError = string.Empty;

    [ObservableProperty]
    private string _lanManualAddress = string.Empty;

    [ObservableProperty]
    private string _lanValidationError = string.Empty;

    [ObservableProperty]
    private bool _localServerRunning;

    public ObservableCollection<ServerRowViewModel> BrowseResults { get; } = [];
    public ObservableCollection<string> LocalListenEndpoints { get; } = [];
    public ObservableCollection<string> RecentAddresses { get; } = [];

    public IReadOnlyList<ServerSortOption> SortOptions { get; } =
    [
        new ServerSortOption { Id = "players-desc", Label = "Players (high to low)" },
        new ServerSortOption { Id = "players-asc", Label = "Players (low to high)" },
        new ServerSortOption { Id = "name", Label = "Name (A to Z)" },
        new ServerSortOption { Id = "version", Label = "Game version" },
    ];

    public IReadOnlyList<ServerVersionFilterOption> VersionFilterOptions { get; } =
    [
        new ServerVersionFilterOption { Id = "any", Label = "Any version" },
        new ServerVersionFilterOption { Id = "active", Label = "Active version" },
        new ServerVersionFilterOption { Id = "compatible", Label = "Compatible major" },
    ];

    public ServersViewModel(
        IMasterServerClient masterServerClient,
        IGameLaunchService launchService,
        IAccountAuthService accountAuth,
        IFavoriteServersStore favorites,
        IRecentServersStore recents,
        IGameServerHost serverHost,
        ILanServerScanner lanScanner,
        IRuntimePlatform platform,
        ITransferTracker transfers,
        IUrlLauncher urlLauncher,
        ILogger<ServersViewModel> logger)
    {
        _masterServerClient = masterServerClient;
        _launchService = launchService;
        _accountAuth = accountAuth;
        _favorites = favorites;
        _recents = recents;
        _serverHost = serverHost;
        _lanScanner = lanScanner;
        _platform = platform;
        _transfers = transfers;
        _urlLauncher = urlLauncher;
        _logger = logger;
        _selectedSortOption = SortOptions[0];
        _selectedVersionFilter = VersionFilterOptions[0];
        _serverHost.StateChanged += (_, _) => RefreshLocalServerState();
    }

    public void Bind(LauncherSettings settings, Action<string>? navigateToSection = null, bool refresh = true)
    {
        _settings = settings;
        _navigateToSection = navigateToSection;
        SelectedSortOption ??= SortOptions[0];
        SelectedVersionFilter ??= VersionFilterOptions[0];
        RefreshLocalServerState();
        if (!_ready)
        {
            _ready = true;
            _ = LoadCatalogAsync(forceNetwork: refresh);
            _ = LoadFavoritesAndRecentsAsync();
            RestartCatalogAutoRefresh();
        }
        else if (refresh)
        {
            _ = LoadCatalogAsync(forceNetwork: true);
        }

        if (IsLanTab)
        {
            RestartLanAutoRefresh();
        }

        _ = RefreshJoinStateAsync();
    }

    [RelayCommand]
    private void OpenOfficialWebList() => _urlLauncher.OpenUrl("https://servers.vintagestory.at/");

    [RelayCommand]
    private void OpenMultiplayerGuide() =>
        _urlLauncher.OpenUrl("https://wiki.vintagestory.at/Guide:Dedicated_Server/en");

    [RelayCommand]
    private void NavigateHosting() => _navigateToSection?.Invoke("hosting");

    [RelayCommand]
    private void NavigateAccountSettings() => _navigateToSection?.Invoke("settings-account");

    partial void OnSearchTextChanged(string value) => ScheduleApplyFilters();

    partial void OnFilterHasPlayersChanged(bool value) => ScheduleApplyFilters();

    partial void OnFilterNoPasswordChanged(bool value) => ScheduleApplyFilters();

    partial void OnFilterNotWhitelistedChanged(bool value) => ScheduleApplyFilters();

    partial void OnFilterVanillaChanged(bool value) => ScheduleApplyFilters();

    partial void OnFilterFavoritesOnlyChanged(bool value) => ScheduleApplyFilters();

    partial void OnSelectedSortOptionChanged(ServerSortOption? value) => ScheduleApplyFilters();

    partial void OnSelectedVersionFilterChanged(ServerVersionFilterOption? value) => ScheduleApplyFilters();

    partial void OnSelectedBrowseServerChanged(ServerRowViewModel? value) => UpdateSelectedDetail();

    partial void OnIsJoiningChanged(bool value) => JoinSelectedCommand.NotifyCanExecuteChanged();

    partial void OnCanJoinChanged(bool value) => JoinSelectedCommand.NotifyCanExecuteChanged();
}
