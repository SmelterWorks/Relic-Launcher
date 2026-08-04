using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Versions;

namespace RelicLauncher.App.ViewModels;

public partial class HomeViewModel : PageViewModelBase
{
    private readonly IGameLaunchService _launchService;
    private readonly IVintageStoryNewsService _newsService;
    private readonly IRemoteNewsImageLoader _imageLoader;
    private readonly IUrlLauncher _urlLauncher;
    private readonly IRuntimePlatform _platform;
    private readonly IInstalledVersionStore _installedStore;
    private readonly ILauncherSettingsStore _settingsStore;
    private readonly ILogger<HomeViewModel> _logger;
    private LauncherSettings _settings = new();
    private Action<LauncherSettings>? _onChanged;
    private bool _bindingVersions;

    [ObservableProperty]
    private bool _canPlay;

    [ObservableProperty]
    private bool _isLoadingNews;

    [ObservableProperty]
    private string _newsStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _showBackgroundLogo;

    [ObservableProperty]
    private Bitmap? _backgroundLogo;

    [ObservableProperty]
    private double _backgroundLogoOpacity = 0.2;

    [ObservableProperty]
    private bool _isShowingArticle;

    [ObservableProperty]
    private bool _isLoadingArticle;

    [ObservableProperty]
    private string _selectedArticleTitle = string.Empty;

    [ObservableProperty]
    private string _selectedArticlePublished = string.Empty;

    [ObservableProperty]
    private string _activeVersionLabel = string.Empty;

    [ObservableProperty]
    private string? _selectedInstalledVersion;

    [ObservableProperty]
    private bool _hasInstalledVersions;

    public ObservableCollection<NewsArticleViewModel> NewsArticles { get; } = [];
    public ObservableCollection<NewsContentBlockViewModel> SelectedArticleBlocks { get; } = [];
    public ObservableCollection<string> InstalledVersions { get; } = [];

    public HomeViewModel(
        IGameLaunchService launchService,
        IVintageStoryNewsService newsService,
        IRemoteNewsImageLoader imageLoader,
        IUrlLauncher urlLauncher,
        IRuntimePlatform platform,
        IInstalledVersionStore installedStore,
        ILauncherSettingsStore settingsStore,
        ILogger<HomeViewModel> logger)
    {
        _launchService = launchService;
        _newsService = newsService;
        _imageLoader = imageLoader;
        _urlLauncher = urlLauncher;
        _platform = platform;
        _installedStore = installedStore;
        _settingsStore = settingsStore;
        _logger = logger;
        StatusMessage = "Install a Vintage Story version on the Versions page to enable Play.";
    }

    public void Bind(LauncherSettings settings, Action<LauncherSettings>? onChanged = null)
    {
        _settings = settings;
        _onChanged = onChanged;
        ApplyLogoSettings(settings);
        IsShowingArticle = false;
        _ = RefreshInstalledVersionsAsync();
        _ = RefreshStatusAsync();
        _ = LoadNewsAsync();
    }

    partial void OnSelectedInstalledVersionChanged(string? value)
    {
        if (_bindingVersions || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (string.Equals(_settings.SelectedVersion, value, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = SetActiveVersionAsync(value);
    }

    private async Task SetActiveVersionAsync(string version)
    {
        _settings.SelectedVersion = version;
        var save = await _settingsStore.SaveAsync(_settings).ConfigureAwait(true);
        if (!save.IsSuccess)
        {
            StatusMessage = save.Error ?? "Could not save selected version.";
            return;
        }

        _onChanged?.Invoke(_settings);
        await RefreshStatusAsync().ConfigureAwait(true);
    }

    private async Task RefreshInstalledVersionsAsync()
    {
        var installsRoot = _settings.InstallsRoot ?? _platform.GetPlatformInfo().DefaultInstallsRoot;
        var installed = await _installedStore.ListAsync(installsRoot).ConfigureAwait(true);
        _bindingVersions = true;
        InstalledVersions.Clear();
        if (installed.IsSuccess)
        {
            foreach (var version in installed.Value!.OrderByDescending(v => v.Version, Comparer<string>.Create(GameVersionComparer.Compare)))
            {
                InstalledVersions.Add(version.Version);
            }
        }

        HasInstalledVersions = InstalledVersions.Count > 0;
        SelectedInstalledVersion = InstalledVersions.FirstOrDefault(v =>
            string.Equals(v, _settings.SelectedVersion, StringComparison.OrdinalIgnoreCase))
            ?? InstalledVersions.FirstOrDefault();
        _bindingVersions = false;
    }

    private async Task RefreshStatusAsync()
    {
        CanPlay = false;
        var installsRoot = _settings.InstallsRoot ?? _platform.GetPlatformInfo().DefaultInstallsRoot;
        var version = _settings.SelectedVersion;
        ActiveVersionLabel = string.IsNullOrWhiteSpace(version) ? "No version selected" : $"Version {version}";

        if (string.IsNullOrWhiteSpace(version))
        {
            StatusMessage = "Install and select a version on the Versions page.";
            return;
        }

        var resolved = await _launchService.ResolveAsync(new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = version,
            DataPath = _settings.DataPath,
        }).ConfigureAwait(true);

        if (!resolved.IsSuccess)
        {
            StatusMessage = resolved.Error ?? "Install path not ready.";
            return;
        }

        var info = resolved.Value!;
        if (!info.ExecutableFound)
        {
            StatusMessage = $"Version {version} is installed but no client executable was found.";
            return;
        }

        CanPlay = true;
        StatusMessage = $"Ready: {info.InstallPath}";
    }

    private async Task LoadNewsAsync()
    {
        var hadArticles = NewsArticles.Count > 0;
        if (!hadArticles)
        {
            IsLoadingNews = true;
        }

        var result = await _newsService.FetchLatestAsync(8).ConfigureAwait(true);
        IsLoadingNews = false;

        if (!result.IsSuccess)
        {
            if (!hadArticles)
            {
                NewsStatusMessage = result.Error ?? "Could not load Vintage Story news.";
            }

            return;
        }

        NewsStatusMessage = string.Empty;
        NewsArticles.Clear();
        foreach (var article in result.Value!)
        {
            NewsArticles.Add(new NewsArticleViewModel(article, ShowArticleAsync));
        }

        if (NewsArticles.Count == 0)
        {
            NewsStatusMessage = "No news entries were found.";
        }
    }

    private async Task ShowArticleAsync(NewsArticleViewModel article)
    {
        IsShowingArticle = true;
        IsLoadingArticle = true;
        SelectedArticleTitle = article.Title;
        SelectedArticlePublished = article.PublishedLabel;
        SelectedArticleBlocks.Clear();

        var result = await _newsService.FetchArticleAsync(article.Url).ConfigureAwait(true);
        IsLoadingArticle = false;

        if (!result.IsSuccess)
        {
            SelectedArticleBlocks.Add(new NewsContentBlockViewModel(
                new NewsContentBlock
                {
                    Kind = NewsContentBlockKind.Text,
                    Text = result.Error ?? "Could not load article.",
                },
                _imageLoader,
                _urlLauncher));
            return;
        }

        var detail = result.Value!;
        SelectedArticleTitle = detail.Title;
        SelectedArticlePublished = detail.PublishedLabel ?? article.PublishedLabel;

        foreach (var block in detail.Blocks)
        {
            var blockVm = new NewsContentBlockViewModel(block, _imageLoader, _urlLauncher);
            SelectedArticleBlocks.Add(blockVm);
            _ = blockVm.LoadImageAsync();
        }

        if (SelectedArticleBlocks.Count == 0 && !string.IsNullOrWhiteSpace(detail.Body))
        {
            SelectedArticleBlocks.Add(new NewsContentBlockViewModel(
                new NewsContentBlock { Kind = NewsContentBlockKind.Text, Text = detail.Body },
                _imageLoader,
                _urlLauncher));
        }
    }

    [RelayCommand]
    private void CloseArticle() => IsShowingArticle = false;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync()
    {
        var installsRoot = _settings.InstallsRoot ?? _platform.GetPlatformInfo().DefaultInstallsRoot;
        var result = await _launchService.LaunchAsync(new GameLaunchRequest
        {
            InstallsRoot = installsRoot,
            Version = _settings.SelectedVersion ?? string.Empty,
            DataPath = _settings.DataPath,
        }).ConfigureAwait(true);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Play failed: {Error}", result.Error);
            StatusMessage = result.Error ?? "Launch failed.";
        }
    }

    partial void OnCanPlayChanged(bool value) => PlayCommand.NotifyCanExecuteChanged();

    private void ApplyLogoSettings(LauncherSettings settings)
    {
        var logo = HomeBackgroundLogoResolver.Resolve(settings);
        ShowBackgroundLogo = logo.ShowLogo;
        BackgroundLogo = logo.ShowLogo ? HomeBackgroundLogoImageLoader.Load(logo.Source) : null;
        BackgroundLogoOpacity = logo.Opacity;
    }
}
