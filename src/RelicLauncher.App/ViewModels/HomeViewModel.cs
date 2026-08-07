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
    private readonly IAccountAuthService _accountAuth;
    private readonly ITransferTracker _transfers;
    private readonly ILogger<HomeViewModel> _logger;
    private LauncherSettings _settings = new();
    private Action<LauncherSettings>? _onChanged;
    private Action<string?>? _navigateToSettings;
    private bool _bindingVersions;

    [ObservableProperty]
    private bool _canPlay;

    [ObservableProperty]
    private bool _isLaunching;

    [ObservableProperty]
    private bool _isSignedIn;

    [ObservableProperty]
    private bool _showSignInPrompt;

    [ObservableProperty]
    private bool _isLoadingNews;

    [ObservableProperty]
    private string _newsStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _newsStatusIsError;

    public string PlayButtonText => IsLaunching ? "Launching..." : "Play";

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
        IAccountAuthService accountAuth,
        ITransferTracker transfers,
        ILogger<HomeViewModel> logger)
    {
        _launchService = launchService;
        _newsService = newsService;
        _imageLoader = imageLoader;
        _urlLauncher = urlLauncher;
        _platform = platform;
        _installedStore = installedStore;
        _settingsStore = settingsStore;
        _accountAuth = accountAuth;
        _transfers = transfers;
        _logger = logger;
        SetStatus("Install a Vintage Story version on the Versions page to enable Play.", true);
    }

    public void Bind(
        LauncherSettings settings,
        Action<LauncherSettings>? onChanged = null,
        Action<string?>? navigateToSettings = null,
        bool refresh = true)
    {
        _settings = settings;
        _onChanged = onChanged;
        _navigateToSettings = navigateToSettings;
        ApplyLogoSettings(settings);
        _ = RefreshAccountStatusAsync();
        _ = RefreshInstalledVersionsAsync();
        _ = RefreshStatusAsync();
        if (refresh)
        {
            IsShowingArticle = false;
            _ = LoadNewsAsync();
        }
    }

    [RelayCommand]
    private void GoToSignIn() => _navigateToSettings?.Invoke("account");

    private async Task RefreshAccountStatusAsync()
    {
        var status = await _accountAuth.GetStatusAsync().ConfigureAwait(true);
        IsSignedIn = status.IsSuccess && status.Value is { IsSignedIn: true };
        ShowSignInPrompt = !IsSignedIn;
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
            SetStatus(save.Error ?? "Could not save selected version.", true);
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
            SetStatus("Install and select a version on the Versions page.", true);
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
            SetStatus(resolved.Error ?? "Install path not ready.", true);
            return;
        }

        var info = resolved.Value!;
        if (!info.ExecutableFound)
        {
            SetStatus($"Version {version} is installed but no client executable was found.", true);
            return;
        }

        var status = await _accountAuth.GetStatusAsync().ConfigureAwait(true);
        if (!status.IsSuccess || status.Value is not { IsSignedIn: true })
        {
            SetStatus("Sign in with your Vintage Story account in Settings to enable Play.", true);
            return;
        }

        CanPlay = true;
        _logger.LogDebug("Play ready for {Version} at {Path}", version, info.InstallPath);
        SetStatus(string.Empty);
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
                NewsStatusIsError = true;
            }

            return;
        }

        NewsStatusMessage = string.Empty;
        NewsStatusIsError = false;
        NewsArticles.Clear();
        foreach (var article in result.Value!)
        {
            NewsArticles.Add(new NewsArticleViewModel(article, ShowArticleAsync));
        }

        if (NewsArticles.Count == 0)
        {
            NewsStatusMessage = "No news entries were found.";
            NewsStatusIsError = false;
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
    private void CloseArticle()
    {
        IsShowingArticle = false;
        SelectedArticleBlocks.Clear();
    }

    [RelayCommand]
    private async Task RetryNewsAsync() => await LoadNewsAsync().ConfigureAwait(true);

    private bool CanExecutePlay() => CanPlay && !IsLaunching;

    [RelayCommand(CanExecute = nameof(CanExecutePlay))]
    private async Task PlayAsync()
    {
        IsLaunching = true;
        SetStatus("Launching...");
        var version = _settings.SelectedVersion ?? string.Empty;
        var runtimeMajor = GameDotNetRuntimeRequirements.TryGetRequiredMajor(version);
        var runtimeLabel = runtimeMajor.IsSuccess
            ? $".NET {runtimeMajor.Value} runtime"
            : ".NET runtime";
        var session = _transfers.Begin(
            $"runtime-play-{version}-{Guid.NewGuid():N}",
            runtimeLabel,
            TransferJobKind.Runtime);

        try
        {
            await session.StartAsync().ConfigureAwait(true);
            var installsRoot = _settings.InstallsRoot ?? _platform.GetPlatformInfo().DefaultInstallsRoot;
            var progress = new Progress<double>(value =>
            {
                session.Report(value);
                SetStatus(value < 1.0
                    ? $"Preparing {runtimeLabel}... {value:P0}"
                    : "Launching...");
            });

            var result = await _launchService.LaunchAsync(new GameLaunchRequest
            {
                InstallsRoot = installsRoot,
                Version = version,
                DataPath = _settings.DataPath,
                Progress = progress,
            }).ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                session.Fail(result.Error ?? "Launch failed.");
                _logger.LogWarning("Play failed: {Error}", result.Error);
                SetStatus(result.Error ?? "Launch failed.", true);
                await RefreshAccountStatusAsync().ConfigureAwait(true);
                await RefreshStatusAsync().ConfigureAwait(true);
            }
            else
            {
                session.Complete($"Ready for {version}");
                SetStatus($"Launched {version}.");
            }
        }
        catch (OperationCanceledException)
        {
            session.Cancel();
            SetStatus("Launch canceled.");
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
            IsLaunching = false;
        }
    }

    partial void OnCanPlayChanged(bool value) => PlayCommand.NotifyCanExecuteChanged();

    partial void OnIsLaunchingChanged(bool value)
    {
        PlayCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PlayButtonText));
    }

    private void ApplyLogoSettings(LauncherSettings settings)
    {
        var logo = HomeBackgroundLogoResolver.Resolve(settings);
        ShowBackgroundLogo = logo.ShowLogo;
        var previous = BackgroundLogo;
        BackgroundLogo = logo.ShowLogo ? HomeBackgroundLogoImageLoader.Load(logo.Source) : null;
        if (!ReferenceEquals(previous, BackgroundLogo))
        {
            OwnedBitmap.DisposeIfOwned(previous);
        }

        BackgroundLogoOpacity = logo.Opacity;
    }

    public void UnloadMedia()
    {
        if (IsShowingArticle)
        {
            CloseArticle();
        }
        else
        {
            SelectedArticleBlocks.Clear();
        }
    }
}
