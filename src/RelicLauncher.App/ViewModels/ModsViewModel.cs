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

namespace RelicLauncher.App.ViewModels;

public partial class ModsViewModel : PageViewModelBase
{
    private const int DefaultPageSize = RelicDefaults.ModBrowsePageSize;
    private readonly IModDbClient _modDb;
    private readonly IModLibraryService _modLibrary;
    private readonly IRuntimePlatform _platform;
    private readonly ITransferTracker _transfers;
    private readonly IRemoteImageCache _images;
    private readonly IUrlLauncher _urlLauncher;
    private readonly ILogger<ModsViewModel> _logger;
    private LauncherSettings _settings = new();
    private CancellationTokenSource? _searchCts;
    private int _activeInstalls;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingDetails;

    [ObservableProperty]
    private bool _filterByActiveVersion = true;

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
    private string _emptyBrowseMessage = "Search ModDB or browse popular mods.";

    [ObservableProperty]
    private string _emptyInstalledMessage = "No mods installed in the data folder yet.";

    [ObservableProperty]
    private Bitmap? _detailLogo;

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

    public ObservableCollection<ModRowViewModel> BrowseResults { get; } = [];
    public ObservableCollection<LocalModInfo> InstalledMods { get; } = [];
    public ObservableCollection<ModImageItemViewModel> ScreenshotItems { get; } = [];
    public ObservableCollection<TransferJobRowViewModel> ActiveTransfers { get; } = [];

    public ModsViewModel(
        IModDbClient modDb,
        IModLibraryService modLibrary,
        IRuntimePlatform platform,
        ITransferTracker transfers,
        IRemoteImageCache images,
        IUrlLauncher urlLauncher,
        ILogger<ModsViewModel> logger)
    {
        _modDb = modDb;
        _modLibrary = modLibrary;
        _platform = platform;
        _transfers = transfers;
        _images = images;
        _urlLauncher = urlLauncher;
        _logger = logger;
        _transfers.Changed += (_, _) => OnTransfersChanged();
        OnTransfersChanged();
    }

    public void Bind(LauncherSettings settings)
    {
        _settings = settings;
        _ = RefreshInstalledAsync();
        _ = SearchAsync();
        _ = _modDb.PrefetchCatalogAsync();
    }

    private string ResolveDataPath()
        => string.IsNullOrWhiteSpace(_settings.DataPath)
            ? _platform.GetPlatformInfo().DefaultDataPath
            : _settings.DataPath!;

    partial void OnFilterByActiveVersionChanged(bool value) => _ = SearchAsync();

    [RelayCommand]
    private async Task SearchAsync()
    {
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
        try
        {
            var result = await _modDb.SearchAsync(new ModSearchQuery
            {
                Text = SearchText,
                GameVersion = FilterByActiveVersion ? _settings.SelectedVersion : null,
                OrderBy = "downloads",
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
                ? $"Showing offline cached catalog ({TotalCount:N0} mods)."
                : $"Showing cached catalog ({TotalCount:N0} mods).";
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
        var result = await _modLibrary.ListInstalledAsync(ResolveDataPath()).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            StatusMessage = result.Error ?? "Could not list installed mods.";
            HasInstalledMods = false;
            return;
        }

        foreach (var mod in result.Value!)
        {
            InstalledMods.Add(mod);
        }

        HasInstalledMods = InstalledMods.Count > 0;
        EmptyInstalledMessage = HasInstalledMods ? string.Empty : "No mods installed in the data folder yet.";
    }

    [RelayCommand]
    private async Task OpenModAsync(ModSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        IsLoadingDetails = true;
        DetailStatus = "Loading details...";
        SelectedDetails = null;
        SelectedRelease = null;
        DetailLogo = null;
        ScreenshotItems.Clear();
        CloseImageViewer();

        var id = !string.IsNullOrWhiteSpace(summary.UrlAlias)
            ? summary.UrlAlias
            : summary.ModId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var result = await _modDb.GetModAsync(id!).ConfigureAwait(true);
        IsLoadingDetails = false;
        if (!result.IsSuccess)
        {
            DetailStatus = result.Error ?? "Could not load mod details.";
            _logger.LogWarning("Mod details failed for {Id}: {Error}", id, result.Error);
            return;
        }

        SelectedDetails = result.Value!;
        SelectedRelease = SelectedDetails.Releases.FirstOrDefault();
        DetailStatus = string.Empty;
        _ = LoadDetailMediaAsync(SelectedDetails);
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

        var release = SelectedRelease ?? SelectedDetails.Releases.FirstOrDefault();
        if (release is null)
        {
            DetailStatus = "No releases available.";
            return;
        }

        var jobId = $"mod-{SelectedDetails.ModId}-{release.FileId}-{Guid.NewGuid():N}";
        var session = _transfers.Begin(jobId, $"Mod {SelectedDetails.Name}", TransferJobKind.Mod);
        try
        {
            await session.StartAsync().ConfigureAwait(true);
            IsInstalling = true;
            _activeInstalls++;
            InstallProgress = 0;
            InstallProgressLabel = $"Downloading {SelectedDetails.Name}...";
            DetailStatus = InstallProgressLabel;

            var progress = new Progress<double>(value =>
            {
                InstallProgress = value;
                session.Report(value);
                InstallProgressLabel = $"Downloading {SelectedDetails.Name}... {value:P0}";
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
