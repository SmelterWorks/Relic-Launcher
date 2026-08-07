using System.Runtime.InteropServices;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.MaterialDesign;
using RelicLauncher.App.Services;
using RelicLauncher.App.ViewModels;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Infrastructure;
using RelicLauncher.Infrastructure.Paths;
using RelicLauncher.Themes;
using Serilog;

namespace RelicLauncher.App;

internal static class Program
{
    private static string? _logsDirectory;

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var pathProvider = new AppPathProvider();
            _logsDirectory = pathProvider.GetPaths().LogsDirectory;
            Directory.CreateDirectory(_logsDirectory);
        }
        catch
        {
            // Best effort only before logging is available.
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (SelfCheck.SelfCheckHost.TryHandle(args, out var selfCheckExitCode))
        {
            return selfCheckExitCode;
        }

        try
        {
            return BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            TryLogFatal(ex);
            return 1;
        }
        finally
        {
            ServiceCollectionExtensions.FlushLogging();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current.Register<MaterialDesignIconProvider>();

        // Stay on X11 unless RELIC_USE_WAYLAND=1 on a real Wayland session.
        // Auto-enabling Avalonia.Wayland from WAYLAND_DISPLAY alone breaks X11
        // sessions and some XWayland setups (window never maps).
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect();

        if (OperatingSystem.IsLinux())
        {
            // Prefer Glx for lower RSS. Virtio/QEMU can need Software: set
            // RELIC_FORCE_SOFTWARE=1 (RELIC_USE_GLX=0 also forces Software).
            var forceSoftware =
                string.Equals(
                    Environment.GetEnvironmentVariable("RELIC_FORCE_SOFTWARE"),
                    "1",
                    StringComparison.Ordinal)
                || string.Equals(
                    Environment.GetEnvironmentVariable("RELIC_USE_GLX"),
                    "0",
                    StringComparison.Ordinal);
            builder = builder.With(new X11PlatformOptions
            {
                RenderingMode = forceSoftware
                    ?
                    [
                        X11RenderingMode.Software,
                        X11RenderingMode.Glx,
                    ]
                    :
                    [
                        X11RenderingMode.Glx,
                        X11RenderingMode.Software,
                    ],
            });

            if (WantsNativeWayland())
            {
                builder = builder.UseWaylandWithFallback();
            }
        }

        return builder
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
    }

    private static bool WantsNativeWayland()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RELIC_USE_WAYLAND"),
                "1",
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(
                Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
                "wayland",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    internal static ServiceProvider BuildServices()
    {
        var pathProvider = new AppPathProvider();
        var debugLogBuffer = new RelicLauncher.Infrastructure.Logging.DebugLogBuffer();
        var loggerFactory = ServiceCollectionExtensions.CreateSerilogLoggerFactory(pathProvider, debugLogBuffer);

        var services = new ServiceCollection();
        services.AddSingleton<IAppPathProvider>(pathProvider);
        services.AddSingleton<RelicLauncher.Core.Abstractions.IDebugLogBuffer>(debugLogBuffer);
        services.AddSingleton(debugLogBuffer);
        services.AddSingleton(loggerFactory);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: false);
        });
        services.AddRelicInfrastructure();
        services.AddSingleton<IThemeCatalog, BuiltInThemeCatalog>();
        services.AddSingleton<IThemeService, AvaloniaThemeService>();
        services.AddSingleton<MainWindowHolder>();
        services.AddSingleton<IStoragePickerService, AvaloniaStoragePickerService>();
        services.AddSingleton<IConfirmDialogService, AvaloniaConfirmDialogService>();
        services.AddSingleton<IRemoteNewsImageLoader, RemoteNewsImageLoader>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<VersionsViewModel>();
        services.AddTransient<ModsViewModel>();
        services.AddSingleton<ModInstallOrchestrator>();
        services.AddSingleton<ModUpdateStartupService>();
        services.AddTransient<ModpackPanelViewModel>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<HostingViewModel>();
        services.AddTransient<WikiViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();

        return services.BuildServiceProvider();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            TryLogFatal(ex);
            CrashReportService.TryShowFatal(ex, _logsDirectory, fileExplorer: null);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        TryLogFatal(e.Exception);
        e.SetObserved();
    }

    private static void TryLogFatal(Exception ex)
    {
        try
        {
            Log.Fatal(ex, "Fatal unhandled exception. Runtime={Runtime} OS={OS}",
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription);
        }
        catch
        {
            // Logging must not throw during crash handling.
        }
    }
}
