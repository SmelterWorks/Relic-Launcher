using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using RelicLauncher.Core;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private string _activeNav = "home";

    [ObservableProperty]
    private double _shellOpacity = 0;

    public string AppVersion => BuildMetadata.Version;

    public Thickness ContentPadding => IsHomeActive ? new Thickness(0) : new Thickness(32, 28, 32, 28);

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
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
        NavigateHome();
        ShellOpacity = 1;
    }

    [RelayCommand]
    private void NavigateHome() => Navigate("home", () =>
    {
        var page = _services.GetRequiredService<HomeViewModel>();
        page.Bind(Settings);
        return page;
    });

    [RelayCommand]
    private void NavigateVersions() => Navigate("versions", () =>
    {
        var page = _services.GetRequiredService<PlaceholderPageViewModel>();
        page.Configure(
            "Versions",
            "Version management is not implemented in this scaffold. This page is a placeholder for install and update flows.");
        return page;
    });

    [RelayCommand]
    private void NavigateMods() => Navigate("mods", () =>
    {
        var page = _services.GetRequiredService<PlaceholderPageViewModel>();
        page.Configure(
            "Mods",
            "Mod browsing and install are not implemented in this scaffold. This page is a placeholder.");
        return page;
    });

    [RelayCommand]
    private void NavigateSettings() => Navigate("settings", () =>
    {
        var page = _services.GetRequiredService<SettingsViewModel>();
        page.Bind(Settings, OnSettingsChanged);
        return page;
    });

    [RelayCommand]
    private void NavigateAbout() => Navigate("about", () => _services.GetRequiredService<AboutViewModel>());

    private void Navigate(string navId, Func<ViewModelBase> createPage)
    {
        ActiveNav = navId;
        CurrentPage = createPage();
        NotifyNavState();
    }

    private void OnSettingsChanged(LauncherSettings settings)
    {
        Settings = settings;
        if (CurrentPage is HomeViewModel home)
        {
            home.Bind(settings);
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
