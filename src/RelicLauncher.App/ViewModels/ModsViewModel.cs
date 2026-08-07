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

public partial class ModsViewModel : PageViewModelBase
{
    private const int DefaultPageSize = RelicDefaults.ModBrowsePageSize;
    private readonly IModDbClient _modDb;
    private readonly IModLibraryService _modLibrary;
    private readonly IModReleaseResolver _releaseResolver;
    private readonly IModDependencyInstallPlanner _dependencyPlanner;
    private readonly IModBlocklistService _blocklist;
    private readonly ModInstallOrchestrator _installOrchestrator;
    private readonly IModUpdateCheckService _updateCheck;
    private readonly IModUpdateStateStore _updateState;
    private readonly IModOriginResolver _originResolver;
    private readonly ILauncherSettingsStore _settingsStore;
    private readonly IRuntimePlatform _platform;
    private readonly ITransferTracker _transfers;
    private readonly IRemoteImageCache _images;
    private readonly IUrlLauncher _urlLauncher;
    private readonly IConfirmDialogService _confirmDialog;
    private readonly IStoragePickerService _storagePicker;
    private readonly IFileExplorerService _fileExplorer;
    private readonly ILogger<ModsViewModel> _logger;
    private LauncherSettings _settings = new();
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _detailCts;
    private int _browseGeneration;
    private int _activeInstalls;
    private bool _ready;
    private readonly HashSet<string> _selectedTagIds = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<ModTagInfo> _allTags = [];
    private readonly List<InstalledModRowViewModel> _allInstalledRows = [];
    private List<LocalModInfo> _installedModInfos = [];
    private readonly Dictionary<string, ModUpdateCandidate> _updateCandidates = new(StringComparer.OrdinalIgnoreCase);
    private bool _viewerOwnsImage;
    private bool _updateCheckScheduled;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingDetails;

    [ObservableProperty]
    private bool _filterByActiveVersion = true;

    [ObservableProperty]
    private ModSortOption? _selectedSortOption;

    [ObservableProperty]
    private ModSideFilterOption? _selectedSideFilter;

    [ObservableProperty]
    private ModDetails? _selectedDetails;

    [ObservableProperty]
    private string _detailStatus = string.Empty;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _pageSize = DefaultPageSize;

    [ObservableProperty]
    private string _pageLabel = string.Empty;

    [ObservableProperty]
    private bool _hasPreviousPage;

    [ObservableProperty]
    private bool _hasNextPage;

    [ObservableProperty]
    private bool _hasBrowseResults;

    [ObservableProperty]
    private bool _hasInstalledMods;

    [ObservableProperty]
    private bool _hasDuplicateMods;

    [ObservableProperty]
    private string _duplicateModsMessage = string.Empty;

    [ObservableProperty]
    private string _emptyBrowseMessage = "Search ModDB or browse popular mods.";

    [ObservableProperty]
    private string _emptyInstalledMessage = "No mods installed in the data folder yet.";

    [ObservableProperty]
    private bool _isSelectedModInstalled;

    [ObservableProperty]
    private string _selectedInstalledLabel = string.Empty;

    [ObservableProperty]
    private Bitmap? _detailLogo = ModIconAssets.Default;

    [ObservableProperty]
    private ModReleaseInfo? _selectedRelease;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _installProgressLabel = string.Empty;

    [ObservableProperty]
    private bool _isImageViewerOpen;

    [ObservableProperty]
    private Bitmap? _viewerImage;

    [ObservableProperty]
    private bool _isViewerLoading;

    [ObservableProperty]
    private bool _hasSelectedTags;

    [ObservableProperty]
    private string _blocklistWarning = string.Empty;

    [ObservableProperty]
    private bool _hasDependencyRows;

    [ObservableProperty]
    private bool _isCheckingUpdates;

    [ObservableProperty]
    private bool _hasAvailableUpdates;

    [ObservableProperty]
    private bool _hasSelectedModUpdate;

    [ObservableProperty]
    private string _selectedUpdateLabel = string.Empty;

    [ObservableProperty]
    private bool _isSelectedModOptedOut;

    [ObservableProperty]
    private string _updateStatusMessage = string.Empty;

    public bool CanUpdateAll => _settings.ModUpdateMode == ModUpdateMode.Prompt && HasAvailableUpdates && !IsCheckingUpdates;
    public bool ShowCheckForUpdates => _settings.ModUpdateMode != ModUpdateMode.Off;

    public ObservableCollection<ModRowViewModel> BrowseResults { get; } = [];
    public ObservableCollection<InstalledModRowViewModel> InstalledMods { get; } = [];
    public ObservableCollection<ModDependencyStatusRowViewModel> DependencyRows { get; } = [];
    public ObservableCollection<ModImageItemViewModel> ScreenshotItems { get; } = [];
    public ObservableCollection<TransferJobRowViewModel> ActiveTransfers { get; } = [];
    public ObservableCollection<ModTagChipViewModel> TagChips { get; } = [];
    public ObservableCollection<string> DetailTagNames { get; } = [];
    public string TagsMenuLabel => HasSelectedTags
        ? $"Tags ({_selectedTagIds.Count})"
        : "Tags";

    public bool CanFilterByActiveVersion => !string.IsNullOrWhiteSpace(_settings.SelectedVersion);

    public string FilterByActiveVersionHint => CanFilterByActiveVersion
        ? string.Empty
        : "Set an active version on the Versions page.";
    public IReadOnlyList<ModSortOption> SortOptions { get; } =
    [
        new ModSortOption { Id = "downloads", Label = "Most downloads" },
        new ModSortOption { Id = "follows", Label = "Most follows" },
        new ModSortOption { Id = "trending", Label = "Trending" },
        new ModSortOption { Id = "updated", Label = "Recently updated" },
        new ModSortOption { Id = "name", Label = "Name A-Z" },
    ];

    public IReadOnlyList<ModSideFilterOption> SideFilterOptions { get; } =
    [
        new ModSideFilterOption { Id = "any", Label = "Any side" },
        new ModSideFilterOption { Id = "client", Label = "Client" },
        new ModSideFilterOption { Id = "server", Label = "Server" },
        new ModSideFilterOption { Id = "both", Label = "Both" },
    ];

    public bool ShowEmptyBrowse => !IsLoading && !HasBrowseResults;

    public ModpackPanelViewModel ModpackPanel { get; }

    public ModsViewModel(
        IModDbClient modDb,
        IModLibraryService modLibrary,
        IModReleaseResolver releaseResolver,
        IModDependencyInstallPlanner dependencyPlanner,
        IModBlocklistService blocklist,
        ModInstallOrchestrator installOrchestrator,
        IModUpdateCheckService updateCheck,
        IModUpdateStateStore updateState,
        IModOriginResolver originResolver,
        ILauncherSettingsStore settingsStore,
        ModpackPanelViewModel modpackPanel,
        IRuntimePlatform platform,
        ITransferTracker transfers,
        IRemoteImageCache images,
        IUrlLauncher urlLauncher,
        IConfirmDialogService confirmDialog,
        IStoragePickerService storagePicker,
        IFileExplorerService fileExplorer,
        ILogger<ModsViewModel> logger)
    {
        _modDb = modDb;
        _modLibrary = modLibrary;
        _releaseResolver = releaseResolver;
        _dependencyPlanner = dependencyPlanner;
        _blocklist = blocklist;
        _installOrchestrator = installOrchestrator;
        _updateCheck = updateCheck;
        _updateState = updateState;
        _originResolver = originResolver;
        _settingsStore = settingsStore;
        ModpackPanel = modpackPanel;
        _platform = platform;
        _transfers = transfers;
        _images = images;
        _urlLauncher = urlLauncher;
        _confirmDialog = confirmDialog;
        _storagePicker = storagePicker;
        _fileExplorer = fileExplorer;
        _logger = logger;
        SelectedSortOption = SortOptions[0];
        SelectedSideFilter = SideFilterOptions[0];
        _transfers.Changed += (_, _) => OnTransfersChanged();
        OnTransfersChanged();
    }

    public void Bind(LauncherSettings settings, bool refresh = true)
    {
        _settings = settings;
        _ready = true;
        OnPropertyChanged(nameof(ShowCheckForUpdates));
        OnPropertyChanged(nameof(CanUpdateAll));
        OnPropertyChanged(nameof(CanFilterByActiveVersion));
        OnPropertyChanged(nameof(FilterByActiveVersionHint));
        ModpackPanel.Bind(settings, refresh);
        _ = RefreshInstalledAsync();
        if (refresh)
        {
            _ = LoadTagsAsync();
            _ = SearchAsync();
            _ = _modDb.PrefetchCatalogAsync();
        }

        ScheduleUpdateCheck(force: false);
    }

    public void ScheduleStartupUpdateCheck()
        => ScheduleUpdateCheck(force: false);

    private void ScheduleUpdateCheck(bool force)
    {
        if (_settings.ModUpdateMode == ModUpdateMode.Off)
        {
            return;
        }

        if (_updateCheckScheduled && !force)
        {
            return;
        }

        _updateCheckScheduled = true;
        _ = RunUpdateCheckAsync(force);
    }

    private string ResolveDataPath()
        => string.IsNullOrWhiteSpace(_settings.DataPath)
            ? _platform.GetPlatformInfo().DefaultDataPath
            : _settings.DataPath!;

    partial void OnFilterByActiveVersionChanged(bool value)
    {
        if (_ready)
        {
            _ = SearchAsync();
        }
    }

    partial void OnSelectedSortOptionChanged(ModSortOption? value)
    {
        if (_ready && value is not null)
        {
            _ = SearchAsync();
        }
    }

    partial void OnSelectedSideFilterChanged(ModSideFilterOption? value)
    {
        if (_ready && value is not null)
        {
            _ = SearchAsync();
        }
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyBrowse));

    partial void OnHasBrowseResultsChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyBrowse));

    private async Task PersistSettingsAsync()
    {
        var save = await _settingsStore.SaveAsync(_settings).ConfigureAwait(true);
        if (!save.IsSuccess)
        {
            SetStatus(save.Error ?? "Could not save settings.", true);
        }
    }

    private static string FormatVersionLabel(string version)
        => string.IsNullOrWhiteSpace(version) ? "?" : version;
    private void OnTransfersChanged()
    {
        void Apply()
        {
            ActiveTransfers.Clear();
            foreach (var job in _transfers.GetJobs().Where(j =>
                         j.Kind == TransferJobKind.Mod &&
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
