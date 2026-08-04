using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Infrastructure.Hosting;
using RelicLauncher.Infrastructure.Logging;
using RelicLauncher.Infrastructure.News;
using RelicLauncher.Infrastructure.Paths;
using RelicLauncher.Infrastructure.Process;
using RelicLauncher.Infrastructure.Settings;
using RelicLauncher.Infrastructure.Stubs;
using Serilog;

namespace RelicLauncher.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRelicInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton<IAppPathProvider, AppPathProvider>();
        services.AddSingleton<ILauncherSettingsStore, JsonLauncherSettingsStore>();
        services.AddSingleton<IGameLocator, GameLocatorStub>();
        services.AddSingleton<IProcessRunner, SafeProcessRunner>();
        services.AddSingleton<IUpdateCheckService, UpdateCheckServiceStub>();
        services.AddSingleton<IVintageStoryNewsService, VintageStoryNewsService>();
        services.AddSingleton<IFileExplorerService, FileExplorerService>();
        services.AddSingleton<IUrlLauncher, UrlLauncher>();
        services.AddSingleton<AppLifetime>();
        services.AddSingleton<IAppLifetime>(sp => sp.GetRequiredService<AppLifetime>());
        return services;
    }

    public static ILoggerFactory CreateSerilogLoggerFactory(IAppPathProvider pathProvider)
    {
        var paths = pathProvider.GetPaths();
        Directory.CreateDirectory(paths.LogsDirectory);

        Log.Logger = SerilogBootstrap.CreateLogger(paths.LogsDirectory);
        return new LoggerFactory().AddSerilog(Log.Logger, dispose: false);
    }

    public static void FlushLogging()
    {
        Log.CloseAndFlush();
    }
}
