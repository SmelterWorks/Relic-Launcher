using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.App.ViewModels;
using RelicLauncher.App.Views;
using RelicLauncher.Core;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure;
using RelicLauncher.Infrastructure.Hosting;
using Serilog;

namespace RelicLauncher.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        _services = Program.BuildServices();

        var logger = _services.GetRequiredService<ILogger<App>>();
        var settings = await LoadSettingsAsync(_services, logger).ConfigureAwait(true);
        ApplyStartupTheme(_services, settings, logger);
        ConfigureDesktopLifetime(_services, settings, logger);

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task<LauncherSettings> LoadSettingsAsync(ServiceProvider services, ILogger<App> logger)
    {
        var settingsStore = services.GetRequiredService<ILauncherSettingsStore>();
        var paths = services.GetRequiredService<IAppPathProvider>().GetPaths();
        Directory.CreateDirectory(paths.RootDirectory);
        Directory.CreateDirectory(paths.LogsDirectory);
        Directory.CreateDirectory(paths.ThemesDirectory);

        var settingsResult = await settingsStore.LoadAsync().ConfigureAwait(true);
        if (!settingsResult.IsSuccess)
        {
            logger.LogWarning("Settings load failed: {Error}. Using defaults.", settingsResult.Error);
            var defaults = new LauncherSettings();
            services.GetRequiredService<IEndpointProvider>().Apply(defaults);
            return defaults;
        }

        var settings = settingsResult.Value!;
        services.GetRequiredService<IEndpointProvider>().Apply(settings);
        return settings;
    }

    private static void ApplyStartupTheme(ServiceProvider services, LauncherSettings settings, ILogger<App> logger)
    {
        var themeService = services.GetRequiredService<IThemeService>();
        var themeResult = themeService.ApplyTheme(settings.SelectedThemeId);
        if (!themeResult.IsSuccess)
        {
            logger.LogWarning("Theme apply failed: {Error}. Falling back to default.", themeResult.Error);
            themeService.ApplyTheme(LauncherSettings.DefaultThemeId);
        }
    }

    private void ConfigureDesktopLifetime(ServiceProvider services, LauncherSettings settings, ILogger<App> logger)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var lifetime = services.GetRequiredService<AppLifetime>();
        var mainVm = services.GetRequiredService<MainWindowViewModel>();
        mainVm.Initialize(settings);

        var windowHolder = services.GetRequiredService<MainWindowHolder>();
        var window = new MainWindow { DataContext = mainVm };
        windowHolder.Window = window;
        lifetime.RegisterShutdownHandler(() => Dispatcher.UIThread.Post(window.Close));

        desktop.ShutdownRequested += (_, _) =>
        {
            try
            {
                lifetime.RequestShutdown();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during shutdown request");
            }
        };

        desktop.Exit += (_, _) =>
        {
            try
            {
                _services?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disposing services");
            }
            finally
            {
                ServiceCollectionExtensions.FlushLogging();
            }
        };

        desktop.MainWindow = window;
        var paths = services.GetRequiredService<IAppPathProvider>().GetPaths();
        logger.LogInformation(
            "Relic Launcher {Version} ({Commit}) started. Logs: {Logs}",
            BuildMetadata.Version,
            BuildMetadata.CommitSha,
            paths.LogsDirectory);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Log.Error(e.Exception, "Dispatcher unhandled exception");
            e.Handled = true;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: Window window })
            {
                var paths = _services?.GetService<IAppPathProvider>()?.GetPaths();
                var fileExplorer = _services?.GetService<IFileExplorerService>();
                _ = CrashReportService.ShowRecoveredAsync(
                    window,
                    e.Exception,
                    paths?.LogsDirectory,
                    fileExplorer);
            }
        }
        catch
        {
            e.Handled = false;
        }
    }
}
