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
    private readonly IModBlocklistService _blocklist;
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
    private int _activeInstalls;
    private bool _ready;
    private readonly HashSet<string> _selectedTagIds = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<ModTagInfo> _allTags = [];
    private readonly List<InstalledModRowViewModel> _allInstalledRows = [];

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
    private string _selectedTagsLabel = string.Empty;

    [ObservableProperty]
    private string _blocklistWarning = string.Empty;

    public ObservableCollection<ModRowViewModel> BrowseResults { get; } = [];
    public ObservableCollection<InstalledModRowViewModel> InstalledMods { get; } = [];
    public ObservableCollection<ModImageItemViewModel> ScreenshotItems { get; } = [];
    public ObservableCollection<TransferJobRowViewModel> ActiveTransfers { get; } = [];
    public ObservableCollection<ModTagChipViewModel> TagChips { get; } = [];
    public ObservableCollection<string> DetailTagNames { get; } = [];
    public string TagsMenuLabel => HasSelectedTags
        ? $"Tags ({_selectedTagIds.Count})"
        : "Tags";
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

    public ModsViewModel(
        IModDbClient modDb,
        IModLibraryService modLibrary,
        IModReleaseResolver releaseResolver,
        IModBlocklistService blocklist,
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
        _blocklist = blocklist;
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
        _ = RefreshInstalledAsync();
        if (refresh)
        {
            _ = LoadTagsAsync();
            _ = SearchAsync();
            _ = _modDb.PrefetchCatalogAsync();
        }
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

    [RelayCommand]
    private async Task SearchAsync()
    {
        ApplyInstalledFilters();
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        Page = 1;
        await LoadPageAsync(token).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (!HasNextPage)
        {
            return;
        }

        Page++;
        await LoadPageAsync(CancellationToken.None).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (!HasPreviousPage)
        {
            return;
        }

        Page--;
        await LoadPageAsync(CancellationToken.None).ConfigureAwait(true);
    }

    private async Task LoadPageAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        BrowseResults.Clear();
        HasBrowseResults = false;
        try
        {
            var orderBy = SelectedSortOption?.Id ?? "downloads";
            var orderDirection = string.Equals(orderBy, "name", StringComparison.OrdinalIgnoreCase)
                ? "asc"
                : "desc";
            var result = await _modDb.SearchAsync(new ModSearchQuery
            {
                Text = SearchText,
                GameVersion = FilterByActiveVersion ? _settings.SelectedVersion : null,
                OrderBy = orderBy,
                OrderDirection = orderDirection,
                Side = SelectedSideFilter?.Id,
                TagIds = _selectedTagIds.ToList(),
                TagNames = TagChips.Where(t => t.IsSelected).Select(t => t.Name).ToList(),
                Page = Page,
                PageSize = DefaultPageSize,
                PreferCache = true,
            }, cancellationToken).ConfigureAwait(true);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                ApplySearchFailure(result.Error);
                return;
            }

            ApplySearchSuccess(result.Value!);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsLoading = false;
            }
        }
    }

    private void ApplySearchFailure(string? error)
    {
        StatusMessage = error ?? "Mod search failed.";
        _logger.LogWarning("Mod search failed: {Error}", error);
        BrowseResults.Clear();
        HasBrowseResults = false;
        EmptyBrowseMessage = "Could not load mods.";
        UpdatePaging(0, Page, DefaultPageSize);
    }

    private void ApplySearchSuccess(ModSearchResult page)
    {
        BrowseResults.Clear();
        foreach (var mod in page.Mods)
        {
            BrowseResults.Add(new ModRowViewModel(mod, _images, OpenModAsync));
        }

        HasBrowseResults = BrowseResults.Count > 0;
        TotalCount = page.TotalCount;
        UpdatePaging(page.TotalCount, page.Page, page.PageSize);
        EmptyBrowseMessage = HasBrowseResults
            ? string.Empty
            : FilterByActiveVersion && !string.IsNullOrWhiteSpace(_settings.SelectedVersion)
                ? $"No mods matched for version {_settings.SelectedVersion}."
                : "No mods matched your search.";

        if (page.FromCache && string.IsNullOrWhiteSpace(StatusMessage) && TotalCount > 0)
        {
            StatusMessage = page.IsStale
                ? "Showing saved ModDB catalog while offline."
                : $"Showing {TotalCount:N0} mods.";
        }

        foreach (var row in BrowseResults)
        {
            _ = row.LoadLogoAsync();
        }
    }

    private void UpdatePaging(int total, int page, int pageSize)
    {
        TotalCount = total;
        Page = page;
        PageSize = pageSize;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(1, pageSize)));
        PageLabel = total == 0 ? "No results" : $"Page {page} of {totalPages} ({total:N0})";
        HasPreviousPage = page > 1;
        HasNextPage = page * pageSize < total;
    }

    [RelayCommand]
    private async Task RefreshInstalledAsync()
    {
        InstalledMods.Clear();
        _allInstalledRows.Clear();
        var result = await _modLibrary.ListInstalledAsync(ResolveDataPath()).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            StatusMessage = result.Error ?? "Could not list installed mods.";
            HasInstalledMods = false;
            HasDuplicateMods = false;
            DuplicateModsMessage = string.Empty;
            EmptyInstalledMessage = "Could not list installed mods.";
            UpdateSelectedInstalledState();
            return;
        }

        var catalog = await LoadCatalogIndexAsync().ConfigureAwait(true);
        foreach (var mod in result.Value!)
        {
            ModSummary? summary = null;
            if (!string.IsNullOrWhiteSpace(mod.ModId))
            {
                catalog.TryGetValue(NormalizeKey(mod.ModId), out summary);
            }

            _allInstalledRows.Add(new InstalledModRowViewModel(mod, summary, _images, _modLibrary));
        }

        UpdateDuplicateState();
        ApplyInstalledFilters();
        UpdateSelectedInstalledState();
    }

    private async Task<Dictionary<string, ModSummary>> LoadCatalogIndexAsync()
    {
        var index = new Dictionary<string, ModSummary>(StringComparer.OrdinalIgnoreCase);
        var result = await _modDb.SearchAsync(new ModSearchQuery
        {
            PreferCache = true,
            Page = 1,
            PageSize = 50_000,
            OrderBy = "name",
            OrderDirection = "asc",
        }).ConfigureAwait(true);
        if (!result.IsSuccess || result.Value is null)
        {
            return index;
        }

        foreach (var mod in result.Value.Mods)
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

        foreach (var row in InstalledMods)
        {
            _ = row.LoadLogoAsync();
        }
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
        var duplicateGroups = _allInstalledRows
            .Where(m => !string.IsNullOrWhiteSpace(m.Info.ModId))
            .GroupBy(m => m.Info.ModId!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();
        HasDuplicateMods = duplicateGroups.Count > 0;
        DuplicateModsMessage = HasDuplicateMods
            ? $"{duplicateGroups.Count} mod id(s) have multiple installs. Clean duplicates to keep one zip per mod."
            : string.Empty;
    }

    private void UpdateSelectedInstalledState()
    {
        if (SelectedDetails is null)
        {
            IsSelectedModInstalled = false;
            SelectedInstalledLabel = string.Empty;
            return;
        }

        var match = _allInstalledRows.FirstOrDefault(row => MatchesInstalled(row.Info, SelectedDetails));
        IsSelectedModInstalled = match is not null;
        SelectedInstalledLabel = match is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(match.Version)
                ? "Already installed"
                : $"Already installed ({match.Version})";
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
            DetailStatus = "This local mod is not linked to ModDB.";
            UpdateSelectedInstalledState();
            return;
        }

        await OpenModByKeyAsync(id).ConfigureAwait(true);
    }

    private async Task OpenModByKeyAsync(string id)
    {
        IsLoadingDetails = true;
        DetailStatus = "Loading details...";
        SelectedDetails = null;
        SelectedRelease = null;
        DetailLogo = ModIconAssets.Default;
        ScreenshotItems.Clear();
        CloseImageViewer();
        UpdateSelectedInstalledState();

        var result = await _modDb.GetModAsync(id).ConfigureAwait(true);
        IsLoadingDetails = false;
        if (!result.IsSuccess)
        {
            DetailStatus = result.Error ?? "Could not load mod details.";
            _logger.LogWarning("Mod details failed for {Id}: {Error}", id, result.Error);
            return;
        }

        SelectedDetails = result.Value!;
        SelectedRelease = await SelectDefaultReleaseAsync(SelectedDetails).ConfigureAwait(true);
        DetailStatus = string.Empty;
        RebuildDetailTags(SelectedDetails);
        UpdateSelectedInstalledState();
        await RefreshBlocklistWarningAsync(SelectedDetails, SelectedRelease).ConfigureAwait(true);
        _ = LoadDetailMediaAsync(SelectedDetails);
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

    private async Task LoadDetailMediaAsync(ModDetails details)
    {
        if (!string.IsNullOrWhiteSpace(details.LogoUrl))
        {
            var bytes = await _images.GetImageBytesAsync(details.LogoUrl).ConfigureAwait(true);
            if (bytes is not null)
            {
                using var stream = new MemoryStream(bytes);
                DetailLogo = new Bitmap(stream);
            }
            else
            {
                DetailLogo = ModIconAssets.Default;
            }
        }
        else
        {
            DetailLogo = ModIconAssets.Default;
        }

        ScreenshotItems.Clear();
        foreach (var shot in details.Screenshots.Take(8))
        {
            var thumbUrl = shot.ThumbnailUrl ?? shot.MainUrl;
            if (string.IsNullOrWhiteSpace(thumbUrl))
            {
                continue;
            }

            var bytes = await _images.GetImageBytesAsync(thumbUrl).ConfigureAwait(true);
            if (bytes is null)
            {
                continue;
            }

            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            ScreenshotItems.Add(new ModImageItemViewModel(bitmap, shot.MainUrl ?? thumbUrl, OpenScreenshotAsync));
        }
    }

    [RelayCommand]
    private async Task OpenDetailLogoAsync()
    {
        if (DetailLogo is null)
        {
            return;
        }

        var url = SelectedDetails?.LogoUrl;
        if (!string.IsNullOrWhiteSpace(url))
        {
            await OpenImageUrlAsync(url).ConfigureAwait(true);
            return;
        }

        ViewerImage = DetailLogo;
        IsImageViewerOpen = true;
    }

    private Task OpenScreenshotAsync(ModImageItemViewModel item)
        => OpenImageUrlAsync(item.FullUrl, item.Thumbnail);

    private async Task OpenImageUrlAsync(string? url, Bitmap? fallback = null)
    {
        IsImageViewerOpen = true;
        IsViewerLoading = true;
        ViewerImage = fallback;
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            var bytes = await _images.GetImageBytesAsync(url).ConfigureAwait(true);
            if (bytes is null)
            {
                return;
            }

            using var stream = new MemoryStream(bytes);
            ViewerImage = new Bitmap(stream);
        }
        finally
        {
            IsViewerLoading = false;
        }
    }

    [RelayCommand]
    private void CloseImageViewer()
    {
        IsImageViewerOpen = false;
        IsViewerLoading = false;
        ViewerImage = null;
    }

    [RelayCommand]
    private async Task InstallSelectedAsync()
    {
        if (SelectedDetails is null)
        {
            DetailStatus = "Select a mod first.";
            return;
        }

        if (IsSelectedModInstalled)
        {
            DetailStatus = SelectedInstalledLabel;
            return;
        }

        var release = SelectedRelease ?? SelectedDetails.Releases.FirstOrDefault();
        if (release is null)
        {
            DetailStatus = "No releases available.";
            return;
        }

        if (!await ConfirmBlockedInstallAsync(SelectedDetails, release).ConfigureAwait(true))
        {
            return;
        }

        await RunModInstallAsync(SelectedDetails, release).ConfigureAwait(true);
    }

    private async Task<bool> ConfirmBlockedInstallAsync(ModDetails details, ModReleaseInfo release)
    {
        if (!_settings.WarnOnBlockedMods)
        {
            return true;
        }

        var modId = ResolveModIdentifier(details);
        var match = await _blocklist.FindMatchAsync(modId, release.ModVersion).ConfigureAwait(true);
        if (!match.IsSuccess || match.Value is null)
        {
            return true;
        }

        var reason = string.IsNullOrWhiteSpace(match.Value.Reason)
            ? match.Value.Id
            : $"{match.Value.Id}: {match.Value.Reason}";
        var proceed = await _confirmDialog.ConfirmAsync(
            "Blocked mod warning",
            $"This release is on the official Vintage Story blocked-mods list ({reason}). Install anyway?",
            "Install anyway",
            "Cancel").ConfigureAwait(true);
        if (!proceed)
        {
            DetailStatus = "Install canceled.";
        }

        return proceed;
    }

    private async Task RunModInstallAsync(ModDetails details, ModReleaseInfo release)
    {
        var jobId = $"mod-{details.ModId}-{release.FileId}-{Guid.NewGuid():N}";
        var session = _transfers.Begin(jobId, $"Mod {details.Name}", TransferJobKind.Mod);
        try
        {
            await session.StartAsync().ConfigureAwait(true);
            IsInstalling = true;
            _activeInstalls++;
            InstallProgress = 0;
            InstallProgressLabel = $"Downloading {details.Name}...";
            DetailStatus = InstallProgressLabel;

            var progress = new Progress<double>(value =>
            {
                InstallProgress = value;
                session.Report(value);
                InstallProgressLabel = $"Downloading {details.Name}... {value:P0}";
                DetailStatus = InstallProgressLabel;
            });

            var result = await _modLibrary.InstallAsync(ResolveDataPath(), release, progress).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                session.Fail(result.Error ?? "Install failed.");
                DetailStatus = result.Error ?? "Install failed.";
                _logger.LogWarning("Mod install failed: {Error}", result.Error);
                return;
            }

            session.Complete($"Installed {result.Value!.FileName}");
            DetailStatus = $"Installed {result.Value.FileName}";
            InstallProgress = 1;
            InstallProgressLabel = DetailStatus;
            await RefreshInstalledAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            session.Cancel();
            DetailStatus = "Install canceled.";
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
            StatusMessage = result.Error ?? "Uninstall failed.";
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

        var result = await _modLibrary.SetEnabledAsync(mod, !mod.IsEnabled).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            StatusMessage = result.Error ?? "Could not change mod state.";
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
            StatusMessage = result.Error ?? "Could not clean duplicate mods.";
            return;
        }

        StatusMessage = result.Value == 0
            ? "No duplicate mods found."
            : $"Removed {result.Value} duplicate mod file(s).";
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
            StatusMessage = result.Error ?? "Could not import local mod.";
            return;
        }

        StatusMessage = $"Imported {result.Value!.FileName}";
        await RefreshInstalledAsync().ConfigureAwait(true);
    }

    private async Task LoadTagsAsync()
    {
        var result = await _modDb.GetTagsAsync().ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            _logger.LogDebug("ModDB tags unavailable: {Error}", result.Error);
            return;
        }

        _allTags = result.Value!;
        RebuildTagChips();
    }

    private void RebuildTagChips()
    {
        TagChips.Clear();
        foreach (var tag in _allTags)
        {
            TagChips.Add(new ModTagChipViewModel(
                tag,
                _selectedTagIds.Contains(tag.TagId),
                OnTagChipToggled));
        }

        UpdateSelectedTagsLabel();
    }

    private void OnTagChipToggled(ModTagChipViewModel chip)
    {
        if (_selectedTagIds.Contains(chip.TagId))
        {
            _selectedTagIds.Remove(chip.TagId);
            chip.IsSelected = false;
        }
        else
        {
            _selectedTagIds.Add(chip.TagId);
            chip.IsSelected = true;
        }

        UpdateSelectedTagsLabel();
        if (_ready)
        {
            _ = SearchAsync();
        }
    }

    [RelayCommand]
    private void ToggleDetailTag(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return;
        }

        var match = _allTags.FirstOrDefault(t =>
            string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return;
        }

        var chip = TagChips.FirstOrDefault(c =>
            string.Equals(c.TagId, match.TagId, StringComparison.OrdinalIgnoreCase));
        if (chip is not null)
        {
            OnTagChipToggled(chip);
            return;
        }

        if (_selectedTagIds.Contains(match.TagId))
        {
            _selectedTagIds.Remove(match.TagId);
        }
        else
        {
            _selectedTagIds.Add(match.TagId);
        }

        RebuildTagChips();
        if (_ready)
        {
            _ = SearchAsync();
        }
    }

    [RelayCommand]
    private void ClearSelectedTags()
    {
        if (_selectedTagIds.Count == 0)
        {
            return;
        }

        _selectedTagIds.Clear();
        foreach (var chip in TagChips)
        {
            chip.IsSelected = false;
        }

        UpdateSelectedTagsLabel();
        if (_ready)
        {
            _ = SearchAsync();
        }
    }

    [RelayCommand]
    private void OpenModFolder(LocalModInfo? mod)
    {
        if (mod is null || string.IsNullOrWhiteSpace(mod.Path))
        {
            return;
        }

        var target = mod.IsDirectory || Directory.Exists(mod.Path)
            ? mod.Path
            : Path.GetDirectoryName(mod.Path);
        if (string.IsNullOrWhiteSpace(target))
        {
            StatusMessage = "Could not resolve mod folder.";
            return;
        }

        var result = _fileExplorer.OpenFolder(target);
        if (!result.IsSuccess)
        {
            StatusMessage = result.Error ?? "Could not open mod folder.";
        }
    }

    [RelayCommand]
    private void OpenModDbPage()
    {
        if (SelectedDetails is null)
        {
            return;
        }

        var url = VintageStoryEndpoints.BuildModDbPageUrl(SelectedDetails.UrlAlias, SelectedDetails.ModId);
        _urlLauncher.OpenUrl(url);
    }

    private void UpdateSelectedTagsLabel()
    {
        HasSelectedTags = _selectedTagIds.Count > 0;
        SelectedTagsLabel = HasSelectedTags
            ? $"{_selectedTagIds.Count} tag filter(s)"
            : string.Empty;
        OnPropertyChanged(nameof(TagsMenuLabel));
    }

    partial void OnSelectedReleaseChanged(ModReleaseInfo? value)
    {
        if (_ready && SelectedDetails is not null)
        {
            _ = RefreshBlocklistWarningAsync(SelectedDetails, value);
        }
    }

    [RelayCommand]
    private void OpenUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            _urlLauncher.OpenUrl(url);
        }
    }

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
