using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using RelicLauncher.App.Services;
using RelicLauncher.Core;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly IEndpointProvider _endpoints;
    private readonly IConfirmDialogService _confirmDialog;
    private readonly Dictionary<string, ViewModelBase> _pageCache = new(StringComparer.Ordinal);

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private string _activeNav = "home";

    [ObservableProperty]
    private double _shellOpacity = 0;

    public string AppVersion => BuildMetadata.Version;

    public Thickness ContentPadding => IsHomeActive ? new Thickness(0) : new Thickness(32, 28, 32, 28);

    public MainWindowViewModel(
        IServiceProvider services,
        IEndpointProvider endpoints,
        IConfirmDialogService confirmDialog)
    {
        _services = services;
        _endpoints = endpoints;
        _confirmDialog = confirmDialog;
    }

    public LauncherSettings Settings { get; private set; } = new();

    public bool IsHomeActive => string.Equals(ActiveNav, "home", StringComparison.Ordinal);
    public bool IsVersionsActive => string.Equals(ActiveNav, "versions", StringComparison.Ordinal);
    public bool IsModsActive => string.Equals(ActiveNav, "mods", StringComparison.Ordinal);
    public bool IsSettingsActive => string.Equals(ActiveNav, "settings", StringComparison.Ordinal);
    public bool IsAboutActive => string.Equals(ActiveNav, "about", StringComparison.Ordinal);

    public void Initialize(LauncherSettings settings)
    {
        Settings = settings;
        _endpoints.Apply(settings);
        NavigateHome();
        ShellOpacity = 1;
    }

    public Task<bool> ConfirmCloseAsync()
    {
        if (!Settings.ConfirmBeforeExit)
        {
            return Task.FromResult(true);
        }

        return _confirmDialog.ConfirmAsync(
            "Exit Relic Launcher",
            "Close Relic Launcher?",
            "Exit",
            "Stay");
    }

    [RelayCommand]
    private void NavigateHome() => Navigate("home", () =>
    {
        var page = _services.GetRequiredService<HomeViewModel>();
        page.Bind(Settings, OnSettingsChanged, NavigateSettings);
        return page;
    }, existing =>
    {
        ((HomeViewModel)existing).Bind(Settings, OnSettingsChanged, NavigateSettings, refresh: false);
    });

    [RelayCommand]
    private void NavigateVersions() => Navigate("versions", () =>
    {
        var page = _services.GetRequiredService<VersionsViewModel>();
        page.Bind(Settings, OnSettingsChanged);
        return page;
    }, existing =>
    {
        ((VersionsViewModel)existing).Bind(Settings, OnSettingsChanged, refresh: false);
    });

    [RelayCommand]
    private void NavigateMods() => Navigate("mods", () =>
    {
        var page = _services.GetRequiredService<ModsViewModel>();
        page.Bind(Settings);
        return page;
    }, existing =>
    {
        ((ModsViewModel)existing).Bind(Settings, refresh: false);
    });

    [RelayCommand]
    private void NavigateSettings() => Navigate("settings", () =>
    {
        var page = _services.GetRequiredService<SettingsViewModel>();
        page.Bind(Settings, OnSettingsChanged);
        return page;
    }, existing =>
    {
        ((SettingsViewModel)existing).Bind(Settings, OnSettingsChanged);
    });

    [RelayCommand]
    private void NavigateAbout() => Navigate("about", () => _services.GetRequiredService<AboutViewModel>());

    private void Navigate(string navId, Func<ViewModelBase> createPage, Action<ViewModelBase>? rebind = null)
    {
        ActiveNav = navId;
        if (_pageCache.TryGetValue(navId, out var cached))
        {
            rebind?.Invoke(cached);
            CurrentPage = cached;
        }
        else
        {
            var page = createPage();
            _pageCache[navId] = page;
            CurrentPage = page;
        }

        NotifyNavState();
    }

    private void OnSettingsChanged(LauncherSettings settings)
    {
        Settings = settings;
        _endpoints.Apply(settings);
        if (CurrentPage is HomeViewModel home)
        {
            home.Bind(settings, OnSettingsChanged, NavigateSettings, refresh: false);
        }
        else if (CurrentPage is VersionsViewModel versions)
        {
            versions.Bind(settings, OnSettingsChanged, refresh: false);
        }
        else if (CurrentPage is ModsViewModel mods)
        {
            mods.Bind(settings, refresh: false);
        }
        else if (CurrentPage is SettingsViewModel settingsPage)
        {
            settingsPage.Bind(settings, OnSettingsChanged);
        }
    }

    partial void OnActiveNavChanged(string value)
    {
        NotifyNavState();
        OnPropertyChanged(nameof(ContentPadding));
    }

    private void NotifyNavState()
    {
        OnPropertyChanged(nameof(IsHomeActive));
        OnPropertyChanged(nameof(IsVersionsActive));
        OnPropertyChanged(nameof(IsModsActive));
        OnPropertyChanged(nameof(IsSettingsActive));
        OnPropertyChanged(nameof(IsAboutActive));
    }
}
