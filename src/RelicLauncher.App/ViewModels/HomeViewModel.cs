using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class HomeViewModel : PageViewModelBase
{
    private readonly IGameLocator _gameLocator;
    private readonly IProcessRunner _processRunner;
    private readonly IVintageStoryNewsService _newsService;
    private readonly IRemoteNewsImageLoader _imageLoader;
    private readonly IUrlLauncher _urlLauncher;
    private readonly ILogger<HomeViewModel> _logger;
    private LauncherSettings _settings = new();
    private string? _resolvedExecutable;

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

    public ObservableCollection<NewsArticleViewModel> NewsArticles { get; } = [];
    public ObservableCollection<NewsContentBlockViewModel> SelectedArticleBlocks { get; } = [];

    public HomeViewModel(
        IGameLocator gameLocator,
        IProcessRunner processRunner,
        IVintageStoryNewsService newsService,
        IRemoteNewsImageLoader imageLoader,
        IUrlLauncher urlLauncher,
        ILogger<HomeViewModel> logger)
    {
        _gameLocator = gameLocator;
        _processRunner = processRunner;
        _newsService = newsService;
        _imageLoader = imageLoader;
        _urlLauncher = urlLauncher;
        _logger = logger;
        StatusMessage = "Set a Vintage Story install path in Settings to enable Play.";
    }

    public void Bind(LauncherSettings settings)
    {
        _settings = settings;
        ApplyLogoSettings(settings);
        IsShowingArticle = false;
        _ = RefreshStatusAsync();
        _ = LoadNewsAsync();
    }

    private async Task RefreshStatusAsync()
    {
        CanPlay = false;
        _resolvedExecutable = null;

        var result = await _gameLocator.LocateAsync(_settings.GameInstallPath).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            StatusMessage = result.Error ?? "Install path not ready.";
            return;
        }

        var info = result.Value!;
        if (!info.ExecutableFound || string.IsNullOrWhiteSpace(info.ExecutablePath))
        {
            StatusMessage = $"Install found at {info.InstallPath}, but no client executable was detected yet.";
            return;
        }

        _resolvedExecutable = info.ExecutablePath;
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
            SelectedArticleBlocks.Clear();
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
        if (string.IsNullOrWhiteSpace(_resolvedExecutable))
        {
            StatusMessage = "No executable to launch.";
            return;
        }

        var result = await _processRunner.StartAsync(_resolvedExecutable, Array.Empty<string>()).ConfigureAwait(true);
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
