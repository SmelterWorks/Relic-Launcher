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
    [STAThread]
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

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
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
    }

    internal static ServiceProvider BuildServices()
    {
        var pathProvider = new AppPathProvider();
        var loggerFactory = ServiceCollectionExtensions.CreateSerilogLoggerFactory(pathProvider);

        var services = new ServiceCollection();
        services.AddSingleton<IAppPathProvider>(pathProvider);
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
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<PlaceholderPageViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();

        return services.BuildServiceProvider();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            TryLogFatal(ex);
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
