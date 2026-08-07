using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.App.Services;
using RelicLauncher.App.ViewModels;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.DotNet;
using RelicLauncher.Infrastructure.Server;
using RelicLauncher.Infrastructure.Transfers;
using RelicLauncher.Infrastructure.Versions;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.App.Tests;

public class HostingViewModelTests
{
    [Fact]
    public void IsLocalHostingSupported_IsFalseOnMacOs()
    {
        var vm = CreateViewModel(HostOs.MacOs);
        vm.IsLocalHostingSupported.Should().BeFalse();
    }

    [Fact]
    public void IsLocalHostingSupported_IsTrueOnLinux()
    {
        var vm = CreateViewModel(HostOs.Linux);
        vm.IsLocalHostingSupported.Should().BeTrue();
    }

    [Fact]
    public void CanStart_IsFalse_WhenNoVersionSelected()
    {
        var vm = CreateViewModel(HostOs.Linux);
        vm.Bind(new LauncherSettings(), _ => { });
        vm.CanStart.Should().BeFalse();
    }

    [Fact]
    public void Bind_OnMacOs_DefaultsToCloudSection()
    {
        var vm = CreateViewModel(HostOs.MacOs);
        vm.Bind(new LauncherSettings(), _ => { }, refresh: false);
        vm.IsCloudSection.Should().BeTrue();
        vm.IsLocalSection.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshAsync_SetsLatestStableServerVersion_FromCatalog()
    {
        var vm = CreateViewModel(HostOs.Linux);
        vm.Bind(new LauncherSettings { InstallsRoot = CreateTempInstallsRoot() }, _ => { }, refresh: false);

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.LatestStableServerVersion.Should().Be("1.22.6");
    }

    [Fact]
    public async Task RefreshAsync_MarksUpgradeAvailable_WhenInstalledVersionIsOlder()
    {
        var installsRoot = CreateTempInstallsRoot();
        SeedInstalledServer(installsRoot, "1.22.5");
        var vm = CreateViewModel(HostOs.Linux);
        vm.Bind(new LauncherSettings
        {
            InstallsRoot = installsRoot,
            SelectedServerVersion = "1.22.5",
        }, _ => { }, refresh: false);

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.HasUpgradeAvailable.Should().BeTrue();
        vm.CanUpgradeToLatest.Should().BeTrue();
        vm.UpgradeTooltip.Should().Contain("1.22.6");
    }

    [Fact]
    public async Task RefreshAsync_DoesNotMarkUpgrade_WhenAlreadyOnLatest()
    {
        var installsRoot = CreateTempInstallsRoot();
        SeedInstalledServer(installsRoot, "1.22.6");
        var vm = CreateViewModel(HostOs.Linux);
        vm.Bind(new LauncherSettings
        {
            InstallsRoot = installsRoot,
            SelectedServerVersion = "1.22.6",
        }, _ => { }, refresh: false);

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.HasUpgradeAvailable.Should().BeFalse();
        vm.CanUpgradeToLatest.Should().BeFalse();
    }

    [Fact]
    public async Task UninstallSelectedVersion_RemovesInstalledVersion_WhenConfirmed()
    {
        var installsRoot = CreateTempInstallsRoot();
        SeedInstalledServer(installsRoot, "1.22.5");
        var confirm = new FakeConfirmDialog(confirm: true);
        var vm = CreateViewModel(HostOs.Linux, confirm);
        vm.Bind(new LauncherSettings
        {
            InstallsRoot = installsRoot,
            SelectedServerVersion = "1.22.5",
        }, _ => { }, refresh: false);
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.UninstallSelectedVersionCommand.ExecuteAsync(null);

        vm.InstalledServerVersions.Should().BeEmpty();
        vm.SelectedInstalledVersion.Should().BeNull();
        vm.StatusMessage.Should().Contain("Removed server 1.22.5");
        Directory.Exists(GameServerInstallLayout.GetServerDirectory(installsRoot, "1.22.5")).Should().BeFalse();
    }

    [Fact]
    public async Task UninstallSelectedVersion_DoesNothing_WhenCanceled()
    {
        var installsRoot = CreateTempInstallsRoot();
        SeedInstalledServer(installsRoot, "1.22.5");
        var confirm = new FakeConfirmDialog(confirm: false);
        var vm = CreateViewModel(HostOs.Linux, confirm);
        vm.Bind(new LauncherSettings
        {
            InstallsRoot = installsRoot,
            SelectedServerVersion = "1.22.5",
        }, _ => { }, refresh: false);
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.UninstallSelectedVersionCommand.ExecuteAsync(null);

        vm.InstalledServerVersions.Should().ContainSingle().Which.Should().Be("1.22.5");
        Directory.Exists(GameServerInstallLayout.GetServerDirectory(installsRoot, "1.22.5")).Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_SetsInstallLatestLabel_WhenLatestNotInstalled()
    {
        var installsRoot = CreateTempInstallsRoot();
        SeedInstalledServer(installsRoot, "1.22.5");
        var vm = CreateViewModel(HostOs.Linux);
        vm.Bind(new LauncherSettings
        {
            InstallsRoot = installsRoot,
            SelectedServerVersion = "1.22.5",
        }, _ => { }, refresh: false);

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.InstallLatestLabel.Should().Be("Install 1.22.6");
        vm.CanInstallLatestServer.Should().BeTrue();
    }

    [Fact]
    public async Task ShowCloudPlanCards_IsHiddenUntilPlansFinishLoading()
    {
        var vm = CreateViewModel(HostOs.MacOs);
        vm.Bind(new LauncherSettings(), _ => { }, refresh: false);
        vm.ShowCloudPlanCards.Should().BeFalse();

        await vm.LoadCloudPlansCommand.ExecuteAsync(null);

        vm.ShowCloudPlanCards.Should().BeTrue();
        vm.CloudPlans.Should().ContainSingle();
    }

    [Fact]
    public async Task LoadCloudPlans_PopulatesPlanCards()
    {
        var vm = CreateViewModel(HostOs.MacOs);
        vm.Bind(new LauncherSettings(), _ => { }, refresh: false);

        await vm.LoadCloudPlansCommand.ExecuteAsync(null);

        vm.CloudPlans.Should().ContainSingle();
        vm.CloudPlans[0].Name.Should().Be("Ember");
    }

    private static string CreateTempInstallsRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "RelicLauncherTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SeedInstalledServer(string installsRoot, string version)
    {
        var dir = GameServerInstallLayout.GetServerDirectory(installsRoot, version);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "VintagestoryServer.dll"), "stub");
        var store = new JsonInstalledServerStore();
        store.SaveAsync(installsRoot,
        [
            new InstalledServerVersion
            {
                Version = version,
                InstallPath = dir,
                ExecutablePath = Path.Combine(dir, "VintagestoryServer.dll"),
                ExecutableFound = true,
                InstalledAt = DateTimeOffset.UtcNow,
            },
        ]).GetAwaiter().GetResult();
    }

    private static HostingViewModel CreateViewModel(HostOs os, IConfirmDialogService? confirmDialog = null)
    {
        var platform = new FakeHostingPlatform(os);
        using var paths = new TempAppPaths();
        var pathProvider = new FixedPathProvider(paths.Paths);
        var clientInstaller = new GameVersionInstaller(
            pathProvider,
            new JsonInstalledVersionStore(),
            platform,
            NullLogger<GameVersionInstaller>.Instance);
        var provisioner = new DotNetRuntimeProvisioner(
            pathProvider,
            platform,
            NullLogger<DotNetRuntimeProvisioner>.Instance);
        var serverHost = new GameServerHost(provisioner, NullLogger<GameServerHost>.Instance);

        return new HostingViewModel(
            platform,
            new NoopFileExplorer(),
            new NoopStoragePicker(),
            new NoopUrlLauncher(),
            new NoopSettingsStore(),
            new FakeHostingCatalog(),
            new JsonInstalledServerStore(),
            new GameServerInstaller(
                pathProvider,
                new JsonInstalledServerStore(),
                platform,
                clientInstaller,
                NullLogger<GameServerInstaller>.Instance),
            serverHost,
            new NoopAccountAuth(),
            provisioner,
            new TransferTracker(),
            new NoopHostingFeed(),
            confirmDialog ?? new FakeConfirmDialog(confirm: true),
            NullLogger<HostingViewModel>.Instance);
    }

    private sealed class FakeHostingPlatform : IRuntimePlatform
    {
        public FakeHostingPlatform(HostOs os) => Os = os;

        public HostOs Os { get; }

        public PlatformInfo GetPlatformInfo()
            => new()
            {
                Os = Os,
                Arch = HostArch.X64,
                ClientPackageKey = Os == HostOs.Windows ? "windows" : "linux",
                ServerPackageKey = Os == HostOs.Windows ? "windowsserver" : "linuxserver",
                DefaultDataPath = "/tmp/data",
                DefaultServerDataPath = "/tmp/server-data",
                DefaultInstallsRoot = "/tmp/installs",
            };
    }

    private sealed class FakeHostingCatalog : IGameVersionCatalog
    {
        public bool LastCatalogWasStale => false;

        public Task<Result<IReadOnlyList<GameVersionInfo>>> GetVersionsAsync(
            GameVersionChannel? channel = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<GameVersionInfo>>.Success(
            [
                CreateVersion("1.22.6"),
                CreateVersion("1.22.5"),
            ]));

        public Task<Result<string?>> GetLatestStableVersionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<string?>.Success("1.22.6"));

        private static GameVersionInfo CreateVersion(string version)
            => new()
            {
                Version = version,
                Channel = GameVersionChannel.Stable,
                Packages =
                [
                    new GameVersionPackage
                    {
                        PlatformKey = "linuxserver",
                        FileName = $"vs_server_linux-x64_{version}.tar.gz",
                        CdnUrl = $"https://cdn.example/{version}.tar.gz",
                        Kind = ClientPackageKind.TarGz,
                    },
                ],
            };
    }

    private sealed class FakeConfirmDialog(bool confirm) : IConfirmDialogService
    {
        public Task<bool> ConfirmAsync(
            string title,
            string message,
            string confirmText = "Confirm",
            string cancelText = "Cancel")
            => Task.FromResult(confirm);
    }

    private sealed class NoopFileExplorer : IFileExplorerService
    {
        public Result OpenFolder(string folderPath) => Result.Success();
    }

    private sealed class NoopStoragePicker : IStoragePickerService
    {
        public Task<string?> PickFolderAsync(string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> PickImageFileAsync(string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> PickZipFileAsync(string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> SaveZipFileAsync(string? suggestedFileName = null, string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> PickModpackFileAsync(string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> SaveModpackFileAsync(string? suggestedFileName = null, string? title = null) => Task.FromResult<string?>(null);
    }

    private sealed class NoopUrlLauncher : IUrlLauncher
    {
        public Result OpenUrl(string url) => Result.Success();
    }

    private sealed class NoopSettingsStore : ILauncherSettingsStore
    {
        public Task<Result<LauncherSettings>> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<LauncherSettings>.Success(new LauncherSettings()));

        public Task<Result> SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class NoopAccountAuth : IAccountAuthService
    {
        public Task<Result<AccountSessionStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AccountSessionStatus>.Success(new AccountSessionStatus()));

        public Task<Result<AccountSessionStatus>> LoginAsync(AccountCredentials credentials, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AccountSessionStatus>.Failure("noop"));

        public Task<Result> LogoutAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> ValidateSessionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class NoopHostingFeed : ISmelterWorksHostingFeedService
    {
        public Task<Result<IReadOnlyList<HostingPlanInfo>>> GetPlansAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<HostingPlanInfo>>.Success(
            [
                new HostingPlanInfo { Name = "Ember", MonthlyPrice = "$10 / month" },
            ]));
    }
}
