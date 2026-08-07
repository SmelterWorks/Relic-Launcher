using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Infrastructure.Auth;
using RelicLauncher.Infrastructure.Backup;
using RelicLauncher.Infrastructure.Caching;
using RelicLauncher.Infrastructure.DotNet;
using RelicLauncher.Infrastructure.Endpoints;
using RelicLauncher.Infrastructure.Hosting;
using RelicLauncher.Infrastructure.Launch;
using RelicLauncher.Infrastructure.Logging;
using RelicLauncher.Infrastructure.Modpacks;
using RelicLauncher.Infrastructure.Mods;
using RelicLauncher.Infrastructure.News;
using RelicLauncher.Infrastructure.Paths;
using RelicLauncher.Infrastructure.Platform;
using RelicLauncher.Infrastructure.Process;
using RelicLauncher.Infrastructure.Sandbox;
using RelicLauncher.Infrastructure.Security;
using RelicLauncher.Infrastructure.Server;
using RelicLauncher.Infrastructure.Servers;
using RelicLauncher.Infrastructure.Settings;
using RelicLauncher.Infrastructure.Stubs;
using RelicLauncher.Infrastructure.Transfers;
using RelicLauncher.Infrastructure.Versions;
using RelicLauncher.Infrastructure.Wiki;
using Serilog;

namespace RelicLauncher.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRelicInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton<IAppPathProvider, AppPathProvider>();
        services.AddSingleton<IRuntimePlatform, RuntimePlatform>();
        services.AddSingleton<IEndpointProvider, EndpointProvider>();
        services.AddSingleton<ILauncherSettingsStore, JsonLauncherSettingsStore>();
        services.AddSingleton<ISecretStore, PlatformSecretStore>();
        services.AddSingleton<AccountAuthService>();
        services.AddSingleton<IAccountAuthService>(sp => sp.GetRequiredService<AccountAuthService>());
        services.AddSingleton<IClientSettingsSessionWriter, ClientSettingsSessionWriter>();
        services.AddSingleton<IGameLocator, GameLocatorStub>();
        AddSandboxServices(services);
        services.AddSingleton<IProcessRunner, SandboxedProcessRunner>();
        services.AddSingleton<IDotNetRuntimeProvisioner, DotNetRuntimeProvisioner>();
        services.AddSingleton<IUpdateCheckService, UpdateCheckServiceStub>();
        services.AddSingleton<IGameVersionCatalog, VintageStoryVersionCatalog>();
        services.AddSingleton<IInstalledVersionStore, JsonInstalledVersionStore>();
        services.AddSingleton<GameVersionInstaller>();
        services.AddSingleton<IGameVersionInstaller>(sp => sp.GetRequiredService<GameVersionInstaller>());
        services.AddSingleton<IGameLaunchService, GameLaunchService>();
        AddServerCatalog(services);
        AddServerHosting(services);
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IModDbClient, ModDbClient>();
        services.AddSingleton<IModReleaseResolver, ModReleaseResolver>();
        services.AddSingleton<IModBlocklistService, ModBlocklistService>();
        services.AddSingleton<IModLibraryService, ModLibraryService>();
        services.AddSingleton<IModDependencyInstallPlanner, ModDependencyInstallPlanner>();
        services.AddSingleton<IModOriginResolver, ModOriginResolver>();
        services.AddSingleton<IModUpdateStateStore, JsonModUpdateStateStore>();
        services.AddSingleton<IModUpdateCheckService, ModUpdateCheckService>();
        services.AddSingleton<IModpackService, ModpackService>();
        services.AddSingleton<ITransferTracker, TransferTracker>();
        services.AddSingleton<IRemoteImageCache, DiskRemoteImageCache>();
        services.AddSingleton<NewsCacheStore>();
        services.AddSingleton<IVintageStoryNewsService, VintageStoryNewsService>();
        services.AddSingleton<IWikiReachabilityProbe, WikiReachabilityProbe>();
        services.AddSingleton<IFileExplorerService, FileExplorerService>();
        services.AddSingleton<IUrlLauncher, UrlLauncher>();
        services.AddSingleton<AppLifetime>();
        services.AddSingleton<IAppLifetime>(sp => sp.GetRequiredService<AppLifetime>());
        return services;
    }

    private static void AddSandboxServices(IServiceCollection services)
    {
        services.AddSingleton<LinuxSandboxLauncher>();
        services.AddSingleton<WindowsAppContainerAclGranter>();
        services.AddSingleton<WindowsAppContainerLauncher>();
        services.AddSingleton<WindowsSandboxLauncher>();
        services.AddSingleton<PassthroughSandboxBrokerClient>();
        services.AddSingleton<SandboxBrokerClient>();
        services.AddSingleton<ISandboxBrokerClient>(sp => sp.GetRequiredService<SandboxBrokerClient>());
        services.AddSingleton<SandboxBrokerHost>();
        services.AddSingleton<SandboxSupport>();
        services.AddSingleton<ISandboxSupport>(sp => sp.GetRequiredService<SandboxSupport>());
        services.AddSingleton<BrokerServerConsole>();
    }

    private static void AddServerCatalog(IServiceCollection services)
    {
        services.AddSingleton<IMasterServerClient, MasterServerClient>();
        services.AddSingleton<IFavoriteServersStore, JsonFavoriteServersStore>();
        services.AddSingleton<IRecentServersStore, JsonRecentServersStore>();
        services.AddSingleton<ILanServerScanner, LanServerScanner>();
    }

    private static void AddServerHosting(IServiceCollection services)
    {
        services.AddSingleton<IInstalledServerStore, JsonInstalledServerStore>();
        services.AddSingleton<IGameServerInstaller, GameServerInstaller>();
        services.AddSingleton<IGameServerHost, GameServerHost>();
        services.AddSingleton<ISmelterWorksHostingFeedService, SmelterWorksHostingFeedService>();
    }

    public static ILoggerFactory CreateSerilogLoggerFactory(IAppPathProvider pathProvider, DebugLogBuffer debugBuffer)
    {
        var paths = pathProvider.GetPaths();
        Directory.CreateDirectory(paths.LogsDirectory);
        Directory.CreateDirectory(paths.CacheDirectory);
        Directory.CreateDirectory(paths.SecretsDirectory);

        Log.Logger = SerilogBootstrap.CreateLogger(paths.LogsDirectory, debugBuffer);
        return new LoggerFactory().AddSerilog(Log.Logger, dispose: false);
    }

    public static void FlushLogging()
    {
        Log.CloseAndFlush();
    }

    public static void DisposeProvider(this ServiceProvider provider)
    {
        provider.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
