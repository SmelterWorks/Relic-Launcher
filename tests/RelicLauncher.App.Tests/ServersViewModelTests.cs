using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.App.Services;
using RelicLauncher.App.ViewModels;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.DotNet;
using RelicLauncher.Infrastructure.Server;
using RelicLauncher.Infrastructure.Servers;
using RelicLauncher.Infrastructure.Transfers;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.App.Tests;

public class ServersViewModelTests
{
    [Fact]
    public async Task SearchText_FiltersBrowseResults()
    {
        var catalog = CreateSampleCatalog();
        var vm = CreateViewModel(new FakeMasterServerClient(catalog), new SignedInAccountAuth());
        vm.Bind(new LauncherSettings { SelectedVersion = "1.22.3" }, null, refresh: false);
        await WaitForCatalogAsync(vm);

        vm.SearchText = "Official";
        await Task.Delay(250);

        vm.BrowseResults.Should().ContainSingle();
        vm.BrowseResults[0].DisplayName.Should().Contain("Official");
    }

    [Fact]
    public async Task StaleCache_ShowsSavedListStatus()
    {
        var catalog = CreateSampleCatalog();
        var fetch = new MasterServerFetchResult
        {
            Catalog = catalog,
            FromCache = true,
            IsStale = true,
            UsedOfficialFallback = false,
        };
        var vm = CreateViewModel(new FakeMasterServerClient(fetch), new SignedInAccountAuth());
        vm.Bind(new LauncherSettings { SelectedVersion = "1.22.3" }, null, refresh: false);
        await WaitForCatalogAsync(vm);

        vm.StatusMessage.Should().Contain("saved server list");
    }

    [Fact]
    public async Task CatalogFailure_ShowsErrorState()
    {
        var vm = CreateViewModel(new FakeMasterServerClient("offline"), new SignedInAccountAuth());
        vm.Bind(new LauncherSettings { SelectedVersion = "1.22.3" }, null, refresh: false);
        await WaitForCatalogAsync(vm);

        vm.ShowCatalogError.Should().BeTrue();
        vm.ShowEmptyBrowse.Should().BeFalse();
        vm.CatalogErrorMessage.Should().Contain("offline");
    }

    [Fact]
    public async Task CanJoin_IsFalse_WhenNotSignedIn()
    {
        var vm = CreateViewModel(new FakeMasterServerClient(CreateSampleCatalog()), new NoopAccountAuth());
        vm.Bind(new LauncherSettings { SelectedVersion = "1.22.3" }, null, refresh: false);
        await WaitForCatalogAsync(vm);

        vm.CanJoin.Should().BeFalse();
    }

    [Fact]
    public async Task CanJoin_IsFalse_WhenNoInstalledVersion()
    {
        var vm = CreateViewModel(new FakeMasterServerClient(CreateSampleCatalog()), new SignedInAccountAuth());
        vm.Bind(new LauncherSettings(), null, refresh: false);
        await WaitForCatalogAsync(vm);

        vm.CanJoin.Should().BeFalse();
        vm.VersionMismatchWarning.Should().Contain("Install a game version");
    }

    [Fact]
    public async Task FilterHasPlayers_ExcludesEmptyServers()
    {
        var catalog = new MasterServerCatalog
        {
            Servers = [
                CreateServer("Busy", "busy.example:42420", players: 5),
                CreateServer("Quiet", "quiet.example:42420", players: 0),
            ],
            FetchedAt = DateTimeOffset.UtcNow,
        };
        var vm = CreateViewModel(new FakeMasterServerClient(catalog), new SignedInAccountAuth());
        vm.Bind(new LauncherSettings { SelectedVersion = "1.22.3" }, null, refresh: false);
        await WaitForCatalogAsync(vm);

        vm.FilterHasPlayers = true;
        await Task.Delay(250);

        vm.BrowseResults.Should().ContainSingle();
        vm.BrowseResults[0].DisplayName.Should().Be("Busy");
    }

    [Fact]
    public async Task FiltersExcludeAll_ShowsEmptyBrowseMessage()
    {
        var catalog = CreateSampleCatalog();
        var vm = CreateViewModel(new FakeMasterServerClient(catalog), new SignedInAccountAuth());
        vm.Bind(new LauncherSettings { SelectedVersion = "1.22.3" }, null, refresh: false);
        await WaitForCatalogAsync(vm);

        vm.SearchText = "no-match-xyz";
        await Task.Delay(250);

        vm.ShowEmptyBrowse.Should().BeTrue();
        vm.EmptyBrowseMessage.Should().Contain("No servers match your filters");
    }

    private static async Task WaitForCatalogAsync(ServersViewModel vm)
    {
        for (var attempt = 0; attempt < 100 && vm.IsLoading; attempt++)
        {
            await Task.Delay(20).ConfigureAwait(false);
        }
    }

    private static MasterServerCatalog CreateSampleCatalog()
    {
        return new MasterServerCatalog
        {
            Servers = [
                CreateServer("The Official Public Server", "tops.vintagestory.at", players: 42),
                CreateServer("Test Server", "127.0.0.1:42420", players: 2),
            ],
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PublicServerSummary CreateServer(string name, string address, int players)
    {
        return new PublicServerSummary
        {
            ServerName = name,
            ServerAddress = address,
            Players = players,
            MaxPlayers = 8,
            GameVersion = "1.22.3",
            HasPassword = false,
            Whitelisted = false,
            ModCount = 0,
            IsOfficialTopS = address.Contains("tops.vintagestory.at", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static ServersViewModel CreateViewModel(
        IMasterServerClient masterServerClient,
        IAccountAuthService accountAuth)
    {
        using var paths = new TempAppPaths();
        var pathProvider = new FixedPathProvider(paths.Paths);
        var provisioner = new DotNetRuntimeProvisioner(
            pathProvider,
            new FakeServersPlatform(),
            NullLogger<DotNetRuntimeProvisioner>.Instance);
        var serverHost = SandboxTestHost.CreateGameServerHost(provisioner);

        return new ServersViewModel(
            masterServerClient,
            new NoopLaunchService(),
            accountAuth,
            new JsonFavoriteServersStore(pathProvider, NullLogger<JsonFavoriteServersStore>.Instance),
            new JsonRecentServersStore(pathProvider, NullLogger<JsonRecentServersStore>.Instance),
            serverHost,
            new FakeLanScanner(),
            new FakeServersPlatform(),
            new TransferTracker(),
            new NoopUrlLauncher(),
            NullLogger<ServersViewModel>.Instance);
    }

    private sealed class FakeLanScanner : ILanServerScanner
    {
        public Task<Result<IReadOnlyList<LanServerSummary>>> ScanAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<LanServerSummary>>.Success(Array.Empty<LanServerSummary>()));
    }

    private sealed class FakeMasterServerClient : IMasterServerClient
    {
        private readonly MasterServerFetchResult? _success;
        private readonly string? _error;

        public FakeMasterServerClient(MasterServerCatalog catalog)
        {
            _success = new MasterServerFetchResult
            {
                Catalog = catalog,
                FromCache = false,
                IsStale = false,
                UsedOfficialFallback = false,
            };
        }

        public FakeMasterServerClient(MasterServerFetchResult fetch)
        {
            _success = fetch;
        }

        public FakeMasterServerClient(string error)
        {
            _error = error;
        }

        public Task<Result<MasterServerFetchResult>> FetchCatalogAsync(
            bool preferCache = true,
            CancellationToken cancellationToken = default)
        {
            if (_error is not null)
            {
                return Task.FromResult(Result<MasterServerFetchResult>.Failure(_error));
            }

            return Task.FromResult(Result<MasterServerFetchResult>.Success(_success!));
        }
    }

    private sealed class FakeServersPlatform : IRuntimePlatform
    {
        public HostOs Os => HostOs.Linux;

        public PlatformInfo GetPlatformInfo()
            => new()
            {
                Os = HostOs.Linux,
                Arch = HostArch.X64,
                ClientPackageKey = "linux",
                ServerPackageKey = "linuxserver",
                DefaultDataPath = "/tmp/data",
                DefaultServerDataPath = "/tmp/server-data",
                DefaultInstallsRoot = "/tmp/installs",
            };
    }

    private sealed class NoopLaunchService : IGameLaunchService
    {
        public Task<Result<GameInstallInfo>> ResolveAsync(
            GameLaunchRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result<GameInstallInfo>.Failure("noop"));

        public Task<Result> LaunchAsync(GameLaunchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class NoopUrlLauncher : IUrlLauncher
    {
        public Result OpenUrl(string url) => Result.Success();
    }

    private sealed class NoopAccountAuth : IAccountAuthService
    {
        public Task<Result<AccountSessionStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AccountSessionStatus>.Success(new AccountSessionStatus()));

        public Task<Result<AccountSessionStatus>> LoginAsync(
            AccountCredentials credentials,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AccountSessionStatus>.Failure("noop"));

        public Task<Result> LogoutAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> ValidateSessionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class SignedInAccountAuth : IAccountAuthService
    {
        public Task<Result<AccountSessionStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AccountSessionStatus>.Success(new AccountSessionStatus
            {
                IsSignedIn = true,
                Email = "player@example.test",
            }));

        public Task<Result<AccountSessionStatus>> LoginAsync(
            AccountCredentials credentials,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AccountSessionStatus>.Failure("noop"));

        public Task<Result> LogoutAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> ValidateSessionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }
}
