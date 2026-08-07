using Microsoft.Extensions.DependencyInjection;
using RelicLauncher.App.Services;
using RelicLauncher.App.ViewModels;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Themes;

namespace RelicLauncher.App;

internal static partial class Program
{
    private static void AddAppServices(IServiceCollection services)
    {
        services.AddSingleton<IThemeCatalog, BuiltInThemeCatalog>();
        services.AddSingleton<IThemeService, AvaloniaThemeService>();
        services.AddSingleton<MainWindowHolder>();
        services.AddSingleton<IStoragePickerService, AvaloniaStoragePickerService>();
        services.AddSingleton<IConfirmDialogService, AvaloniaConfirmDialogService>();
        services.AddSingleton<IRemoteNewsImageLoader, RemoteNewsImageLoader>();
        services.AddSingleton<ToastHostViewModel>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<LauncherUpdateCoordinator>();
        services.AddSingleton<LauncherUpdateStartupService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<VersionsViewModel>();
        services.AddTransient<ModsViewModel>();
        services.AddSingleton<ModInstallOrchestrator>();
        services.AddSingleton<ModUpdateStartupService>();
        services.AddTransient<ModpackPanelViewModel>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<ServersViewModel>();
        services.AddTransient<HostingViewModel>();
        services.AddTransient<WikiViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();
    }
}
