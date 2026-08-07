using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.SelfCheck;
using RelicLauncher.Infrastructure;
using RelicLauncher.Infrastructure.Platform;
using RelicLauncher.Infrastructure.Server;
using RelicLauncher.Infrastructure.Settings;

namespace RelicLauncher.Infrastructure.SelfCheck;

public sealed class RelicSelfCheckRunner
{
    private static readonly Regex VersionPattern = new(@"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private readonly string _dataRoot;
    private readonly bool _includeNetwork;

    public RelicSelfCheckRunner(string dataRoot, bool includeNetwork = true)
    {
        _dataRoot = dataRoot;
        _includeNetwork = includeNetwork;
    }

    public async Task<SelfCheckReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<SelfCheckItem>
        {
            CheckVersion(),
            CheckRuntime(),
        };

        items.Add(await CheckStartupAsync(cancellationToken).ConfigureAwait(false));
        items.Add(await CheckPathsAsync(cancellationToken).ConfigureAwait(false));
        items.Add(await CheckReadWriteAsync(cancellationToken).ConfigureAwait(false));
        items.Add(await CheckSettingsAsync(cancellationToken).ConfigureAwait(false));
        items.Add(CheckPlatform());
        items.Add(await CheckServerLayoutAsync(cancellationToken).ConfigureAwait(false));
        items.Add(await CheckServerInventoryAsync(cancellationToken).ConfigureAwait(false));
        items.Add(await CheckServerEndpointsAsync(cancellationToken).ConfigureAwait(false));
        items.Add(CheckServerPackageSelection());

        if (_includeNetwork)
        {
            items.Add(await CheckVersionCatalogAsync(cancellationToken).ConfigureAwait(false));
            items.Add(await CheckServerListAsync(cancellationToken).ConfigureAwait(false));
            items.Add(await CheckHostingFeedAsync(cancellationToken).ConfigureAwait(false));
        }
        else
        {
            items.Add(SelfCheckItem.Skip("catalog", "Version catalog", "Skipped with --no-network"));
            items.Add(SelfCheckItem.Skip("server-list", "Public server list", "Skipped with --no-network"));
            items.Add(SelfCheckItem.Skip("hosting-feed", "Hosting feed", "Skipped with --no-network"));
        }

        return new SelfCheckReport(items);
    }

    private SelfCheckItem CheckVersion()
    {
        var version = BuildMetadata.Version;
        if (string.IsNullOrWhiteSpace(version) || !VersionPattern.IsMatch(version))
        {
            return SelfCheckItem.Fail("version", "Build version", $"Unexpected version '{version}'");
        }

        return SelfCheckItem.Pass("version", "Build version", version);
    }

    private static SelfCheckItem CheckRuntime()
    {
        var platform = new RuntimePlatform().GetPlatformInfo();
        if (platform.Os is HostOs.Unknown || platform.Arch is HostArch.Unknown)
        {
            return SelfCheckItem.Fail(
                "runtime",
                "Runtime platform",
                $"OS={platform.Os}, arch={platform.Arch}");
        }

        var detail = $"{RuntimeInformation.FrameworkDescription}; OS={platform.Os}; arch={platform.Arch}";
        return SelfCheckItem.Pass("runtime", "Runtime platform", detail);
    }

    private async Task<SelfCheckItem> CheckStartupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var provider = BuildServiceProvider();
            _ = provider.GetRequiredService<IRuntimePlatform>();
            _ = provider.GetRequiredService<ILauncherSettingsStore>();
            _ = provider.GetRequiredService<IGameVersionCatalog>();
            _ = provider.GetRequiredService<IInstalledServerStore>();
            _ = provider.GetRequiredService<IGameServerInstaller>();
            _ = provider.GetRequiredService<IGameServerHost>();
            _ = provider.GetRequiredService<ISmelterWorksHostingFeedService>();
            return SelfCheckItem.Pass("startup", "Service startup", "Core services resolved");
        }
        catch (Exception ex)
        {
            return SelfCheckItem.Fail("startup", "Service startup", ex.Message);
        }
    }

    private async Task<SelfCheckItem> CheckPathsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var paths = new SelfCheckAppPathProvider(_dataRoot).GetPaths();
            Directory.CreateDirectory(paths.RootDirectory);
            Directory.CreateDirectory(paths.LogsDirectory);
            Directory.CreateDirectory(paths.CacheDirectory);
            Directory.CreateDirectory(paths.SecretsDirectory);
            Directory.CreateDirectory(paths.ThemesDirectory);
            var installsRoot = Path.Combine(_dataRoot, "installs");
            Directory.CreateDirectory(GameServerInstallLayout.GetServersRoot(installsRoot));
            await Task.CompletedTask.ConfigureAwait(false);
            return SelfCheckItem.Pass("paths", "App directories", paths.RootDirectory);
        }
        catch (Exception ex)
        {
            return SelfCheckItem.Fail("paths", "App directories", ex.Message);
        }
    }

    private async Task<SelfCheckItem> CheckReadWriteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var paths = new SelfCheckAppPathProvider(_dataRoot).GetPaths();
            foreach (var directory in new[] { paths.CacheDirectory, paths.LogsDirectory, paths.RootDirectory })
            {
                Directory.CreateDirectory(directory);
                var filePath = Path.Combine(directory, $"self-check-{Guid.NewGuid():N}.txt");
                await File.WriteAllTextAsync(filePath, "relic-self-check", cancellationToken).ConfigureAwait(false);
                var text = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(text, "relic-self-check", StringComparison.Ordinal))
                {
                    return SelfCheckItem.Fail("read-write", "Directory permissions", $"Read mismatch in {directory}");
                }

                File.Delete(filePath);
            }

            return SelfCheckItem.Pass("read-write", "Directory permissions", "Read, write, and delete succeeded");
        }
        catch (Exception ex)
        {
            return SelfCheckItem.Fail("read-write", "Directory permissions", ex.Message);
        }
    }

    private async Task<SelfCheckItem> CheckSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var provider = BuildServiceProvider();
            var store = provider.GetRequiredService<ILauncherSettingsStore>();
            var loaded = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!loaded.IsSuccess)
            {
                return SelfCheckItem.Fail("settings", "Settings store", loaded.Error ?? "Load failed");
            }

            loaded.Value!.InstallsRoot = Path.Combine(_dataRoot, "installs");
            var saved = await store.SaveAsync(loaded.Value, cancellationToken).ConfigureAwait(false);
            if (!saved.IsSuccess)
            {
                return SelfCheckItem.Fail("settings", "Settings store", saved.Error ?? "Save failed");
            }

            var reloaded = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!reloaded.IsSuccess
                || !string.Equals(reloaded.Value!.InstallsRoot, loaded.Value.InstallsRoot, StringComparison.Ordinal))
            {
                return SelfCheckItem.Fail("settings", "Settings store", "Round-trip mismatch");
            }

            return SelfCheckItem.Pass("settings", "Settings store", reloaded.Value.InstallsRoot);
        }
        catch (Exception ex)
        {
            return SelfCheckItem.Fail("settings", "Settings store", ex.Message);
        }
    }

    private static SelfCheckItem CheckPlatform()
    {
        var platform = new RuntimePlatform().GetPlatformInfo();
        if (string.IsNullOrWhiteSpace(platform.ClientPackageKey)
            || string.IsNullOrWhiteSpace(platform.ServerPackageKey))
        {
            return SelfCheckItem.Fail("platform", "Platform package keys", "Missing client or server package key");
        }

        var localHosting = platform.Os is HostOs.Windows or HostOs.Linux;
        var detail = $"client={platform.ClientPackageKey}; server={platform.ServerPackageKey}; localHosting={localHosting}";
        return SelfCheckItem.Pass("platform", "Platform package keys", detail);
    }

    private async Task<SelfCheckItem> CheckServerLayoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            var installsRoot = Path.Combine(_dataRoot, "installs");
            var version = "1.22.6";
            var serverDir = GameServerInstallLayout.GetServerDirectory(installsRoot, version);
            Directory.CreateDirectory(serverDir);
            var exePath = Path.Combine(serverDir, "VintagestoryServer.dll");
            await File.WriteAllTextAsync(exePath, "stub", cancellationToken).ConfigureAwait(false);

            var exe = VintageStoryServerExecutableLocator.FindServerExecutable(serverDir);
            if (exe is null)
            {
                return SelfCheckItem.Fail("server-layout", "Server install layout", "Server executable not found");
            }

            return SelfCheckItem.Pass("server-layout", "Server install layout", serverDir);
        }
        catch (Exception ex)
        {
            return SelfCheckItem.Fail("server-layout", "Server install layout", ex.Message);
        }
    }

    private async Task<SelfCheckItem> CheckServerInventoryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var installsRoot = Path.Combine(_dataRoot, "installs");
            using var provider = BuildServiceProvider();
            var store = provider.GetRequiredService<IInstalledServerStore>();
            var list = await store.ListAsync(installsRoot, cancellationToken).ConfigureAwait(false);
            if (!list.IsSuccess || list.Value!.Count == 0)
            {
                return SelfCheckItem.Fail("server-inventory", "Server inventory", list.Error ?? "No installed servers found");
            }

            return SelfCheckItem.Pass("server-inventory", "Server inventory", $"{list.Value.Count} version(s)");
        }
        catch (Exception ex)
        {
            return SelfCheckItem.Fail("server-inventory", "Server inventory", ex.Message);
        }
    }

    private async Task<SelfCheckItem> CheckServerEndpointsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var dataPath = Path.Combine(_dataRoot, "server-data");
            Directory.CreateDirectory(dataPath);
            await File.WriteAllTextAsync(
                Path.Combine(dataPath, "serverconfig.json"),
                """{ "Port": 42421 }""",
                cancellationToken).ConfigureAwait(false);

            var endpoints = ServerListenEndpointResolver.Resolve(dataPath);
            if (endpoints.Count == 0 || !endpoints.Any(static e => e.EndsWith(":42421", StringComparison.Ordinal)))
            {
                return SelfCheckItem.Fail("server-endpoints", "Server listen endpoints", "No endpoints resolved");
            }

            return SelfCheckItem.Pass("server-endpoints", "Server listen endpoints", string.Join(", ", endpoints.Take(3)));
        }
        catch (Exception ex)
        {
            return SelfCheckItem.Fail("server-endpoints", "Server listen endpoints", ex.Message);
        }
    }

    private SelfCheckItem CheckServerPackageSelection()
    {
        try
        {
            using var provider = BuildServiceProvider();
            var installer = provider.GetRequiredService<IGameServerInstaller>();
            var platform = provider.GetRequiredService<IRuntimePlatform>().GetPlatformInfo();
            var version = new GameVersionInfo
            {
                Version = "1.22.6",
                Channel = GameVersionChannel.Stable,
                Packages =
                [
                    new GameVersionPackage
                    {
                        PlatformKey = platform.ServerPackageKey,
                        FileName = $"vs_server_{platform.ServerPackageKey}_1.22.6.tar.gz",
                        CdnUrl = "https://cdn.example.test/server.tar.gz",
                        Kind = ClientPackageKind.TarGz,
                    },
                ],
            };

            var package = installer.SelectServerPackage(version, platform);
            if (package is null)
            {
                return SelfCheckItem.Fail("server-package", "Server package selection", $"No package for {platform.ServerPackageKey}");
            }

            return SelfCheckItem.Pass("server-package", "Server package selection", package.PlatformKey);
        }
        catch (Exception ex)
        {
            return SelfCheckItem.Fail("server-package", "Server package selection", ex.Message);
        }
    }

    private Task<SelfCheckItem> CheckVersionCatalogAsync(CancellationToken cancellationToken)
        => SelfCheckCatalogProbe.RunAsync(BuildServiceProvider, cancellationToken);

    private async Task<SelfCheckItem> CheckServerListAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var provider = BuildServiceProvider();
            var client = provider.GetRequiredService<IMasterServerClient>();
            var result = await client.FetchCatalogAsync(preferCache: true, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return SelfCheckItem.Fail("server-list", "Public server list", result.Error ?? "Fetch failed");
            }

            return SelfCheckItem.Pass(
                "server-list",
                "Public server list",
                $"{result.Value!.Catalog.Servers.Count} server(s)");
        }
        catch (Exception ex)
        {
            return SelfCheckItem.Fail("server-list", "Public server list", ex.Message);
        }
    }

    private async Task<SelfCheckItem> CheckHostingFeedAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var provider = BuildServiceProvider();
            var feed = provider.GetRequiredService<ISmelterWorksHostingFeedService>();
            var plans = await feed.GetPlansAsync(cancellationToken).ConfigureAwait(false);
            if (!plans.IsSuccess || plans.Value!.Count == 0)
            {
                return SelfCheckItem.Fail("hosting-feed", "Hosting feed", plans.Error ?? "No plans returned");
            }

            return SelfCheckItem.Pass("hosting-feed", "Hosting feed", $"{plans.Value.Count} plan(s)");
        }
        catch (Exception ex)
        {
            return SelfCheckItem.Fail("hosting-feed", "Hosting feed", ex.Message);
        }
    }

    private ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAppPathProvider>(new SelfCheckAppPathProvider(_dataRoot));
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddRelicInfrastructure();
        return services.BuildServiceProvider();
    }
}
