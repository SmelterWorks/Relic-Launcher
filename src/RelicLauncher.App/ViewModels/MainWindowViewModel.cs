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
    private readonly IGameServerHost _serverHost;
    private readonly IRuntimePlatform _platform;
    private readonly Dictionary<string, ViewModelBase> _pageCache = new(StringComparer.Ordinal);

    public ToastHostViewModel ToastHost { get; }

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private string _activeNav = "home";

    [ObservableProperty]
    private double _shellOpacity = 1;

    public string AppVersion => BuildMetadata.Version;

    public MainWindowViewModel(
        IServiceProvider services,
        IEndpointProvider endpoints,
        IConfirmDialogService confirmDialog,
        IGameServerHost serverHost,
        IRuntimePlatform platform,
        ToastHostViewModel toastHost)
    {
        _services = services;
        _endpoints = endpoints;
        _confirmDialog = confirmDialog;
        _serverHost = serverHost;
        _platform = platform;
        ToastHost = toastHost;
    }

    public LauncherSettings Settings { get; private set; } = new();

    public bool IsHostingSupported => true;
    public bool IsHomeActive => string.Equals(ActiveNav, "home", StringComparison.Ordinal);
    public bool IsVersionsActive => string.Equals(ActiveNav, "versions", StringComparison.Ordinal);
    public bool IsModsActive => string.Equals(ActiveNav, "mods", StringComparison.Ordinal);
    public bool IsBackupActive => string.Equals(ActiveNav, "backup", StringComparison.Ordinal);
    public bool IsServersActive => string.Equals(ActiveNav, "servers", StringComparison.Ordinal);
    public bool IsHostingActive => string.Equals(ActiveNav, "hosting", StringComparison.Ordinal);
    public bool IsWikiActive => string.Equals(ActiveNav, "wiki", StringComparison.Ordinal);
    public bool IsSettingsActive => string.Equals(ActiveNav, "settings", StringComparison.Ordinal);
    public bool IsAboutActive => string.Equals(ActiveNav, "about", StringComparison.Ordinal);

    public void Initialize(LauncherSettings settings)
    {
        Settings = settings;
        _endpoints.Apply(settings);
        NavigateHome();
        ShellOpacity = 1;
    }

    public void ApplySettings(LauncherSettings settings)
    {
        Settings = settings;
        _endpoints.Apply(settings);
        OnSettingsChanged(settings);
    }

    public Task<bool> ConfirmCloseAsync()
    {
        return ConfirmCloseCoreAsync();
    }

    private async Task<bool> ConfirmCloseCoreAsync()
    {
        if (_serverHost.State is ServerProcessState.Running or ServerProcessState.Starting or ServerProcessState.Stopping)
        {
            await _serverHost.StopAsync().ConfigureAwait(true);
        }

        if (!Settings.ConfirmBeforeExit)
        {
            return true;
        }

        return await _confirmDialog.ConfirmAsync(
            "Exit Relic Launcher",
            "Close Relic Launcher?",
            "Exit",
            "Stay").ConfigureAwait(true);
    }

    [RelayCommand]
    private void NavigateHome() => Navigate("home", () =>
    {
        var page = _services.GetRequiredService<HomeViewModel>();
        page.Bind(
            Settings,
            OnSettingsChanged,
            section => NavigateSettingsInternal(section),
            () => NavigateVersionsCommand.Execute(null));
        return page;
    }, existing =>
    {
        ((HomeViewModel)existing).Bind(
            Settings,
            OnSettingsChanged,
            section => NavigateSettingsInternal(section),
            () => NavigateVersionsCommand.Execute(null),
            refresh: false);
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
    private void NavigateBackup() => Navigate("backup", () =>
    {
        var page = _services.GetRequiredService<BackupViewModel>();
        page.Bind(Settings);
        return page;
    }, existing =>
    {
        ((BackupViewModel)existing).Bind(Settings, refresh: false);
    });

    [RelayCommand]
    private void NavigateServers() => Navigate("servers", () =>
    {
        var page = _services.GetRequiredService<ServersViewModel>();
        page.Bind(Settings, NavigateServersSection);
        return page;
    }, existing =>
    {
        ((ServersViewModel)existing).Bind(Settings, NavigateServersSection, refresh: false);
    });

    private void NavigateServersSection(string section)
    {
        if (string.Equals(section, "hosting", StringComparison.Ordinal))
        {
            NavigateHosting();
        }
        else if (string.Equals(section, "settings-account", StringComparison.Ordinal))
        {
            NavigateSettingsInternal("account");
        }
    }

    [RelayCommand]
    private void NavigateHosting() => Navigate("hosting", () =>
    {
        var page = _services.GetRequiredService<HostingViewModel>();
        page.Bind(Settings, OnSettingsChanged);
        return page;
    }, existing =>
    {
        ((HostingViewModel)existing).Bind(Settings, OnSettingsChanged, refresh: false);
    });

    [RelayCommand]
    private void NavigateWiki() => Navigate("wiki", () =>
    {
        var page = _services.GetRequiredService<WikiViewModel>();
        page.Bind(Settings);
        return page;
    }, existing =>
    {
        ((WikiViewModel)existing).Bind(Settings, refresh: false);
    });

    [RelayCommand]
    private void NavigateSettings() => NavigateSettingsInternal(null);

    private void NavigateSettingsInternal(string? focusSection) => Navigate("settings", () =>
    {
        var page = _services.GetRequiredService<SettingsViewModel>();
        page.Bind(Settings, OnSettingsChanged);
        if (string.Equals(focusSection, "account", StringComparison.Ordinal))
        {
            page.RequestFocusAccount();
        }

        return page;
    }, existing =>
    {
        ((SettingsViewModel)existing).Bind(Settings, OnSettingsChanged);
        if (string.Equals(focusSection, "account", StringComparison.Ordinal))
        {
            ((SettingsViewModel)existing).RequestFocusAccount();
        }
    });

    [RelayCommand]
    private void NavigateAbout() => Navigate("about", () =>
    {
        var page = _services.GetRequiredService<AboutViewModel>();
        page.Bind(Settings, ApplySettings);
        return page;
    }, existing =>
    {
        ((AboutViewModel)existing).Bind(Settings, ApplySettings);
    });

    private void Navigate(string navId, Func<ViewModelBase> createPage, Action<ViewModelBase>? rebind = null)
    {
        if (!string.Equals(ActiveNav, navId, StringComparison.Ordinal))
        {
            UnloadPageMedia(CurrentPage);
        }

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

    private static void UnloadPageMedia(ViewModelBase? page)
    {
        switch (page)
        {
            case ModsViewModel mods:
                mods.UnloadMedia();
                break;
            case HomeViewModel home:
                home.UnloadMedia();
                break;
        }
    }

    private void OnSettingsChanged(LauncherSettings settings)
    {
        Settings = settings;
        _endpoints.Apply(settings);
        if (CurrentPage is HomeViewModel home)
        {
            home.Bind(
                settings,
                OnSettingsChanged,
                section => NavigateSettingsInternal(section),
                () => NavigateVersionsCommand.Execute(null),
                refresh: false);
        }
        else if (CurrentPage is VersionsViewModel versions)
        {
            versions.Bind(settings, OnSettingsChanged, refresh: false);
        }
        else if (CurrentPage is ModsViewModel mods)
        {
            mods.Bind(settings, refresh: false);
        }
        else if (CurrentPage is BackupViewModel backup)
        {
            backup.Bind(settings, refresh: false);
        }
        else if (CurrentPage is ServersViewModel servers)
        {
            servers.Bind(settings, NavigateServersSection, refresh: false);
        }
        else if (CurrentPage is HostingViewModel hosting)
        {
            hosting.Bind(settings, OnSettingsChanged, refresh: false);
        }
        else if (CurrentPage is WikiViewModel wiki)
        {
            wiki.Bind(settings, refresh: false);
        }
        else if (CurrentPage is SettingsViewModel settingsPage)
        {
            settingsPage.Bind(settings, OnSettingsChanged);
        }
        else if (CurrentPage is AboutViewModel about)
        {
            about.Bind(settings, ApplySettings);
        }
    }

    partial void OnActiveNavChanged(string value)
    {
        NotifyNavState();
    }

    private void NotifyNavState()
    {
        OnPropertyChanged(nameof(IsHomeActive));
        OnPropertyChanged(nameof(IsVersionsActive));
        OnPropertyChanged(nameof(IsModsActive));
        OnPropertyChanged(nameof(IsBackupActive));
        OnPropertyChanged(nameof(IsServersActive));
        OnPropertyChanged(nameof(IsHostingActive));
        OnPropertyChanged(nameof(IsHostingSupported));
        OnPropertyChanged(nameof(IsWikiActive));
        OnPropertyChanged(nameof(IsSettingsActive));
        OnPropertyChanged(nameof(IsAboutActive));
    }
}
