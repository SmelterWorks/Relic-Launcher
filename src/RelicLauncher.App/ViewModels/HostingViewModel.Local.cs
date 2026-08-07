using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Versions;

namespace RelicLauncher.App.ViewModels;

public partial class HostingViewModel
{
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!IsLocalHostingSupported)
        {
            return;
        }

        IsBusy = true;
        ProgressLabel = "Refreshing server list...";
        SetStatus(string.Empty);
        try
        {
            await LoadInstalledVersionsAsync().ConfigureAwait(true);
            ProgressLabel = "Checking catalog for server versions...";
            var catalogLoaded = await LoadCatalogVersionsAsync().ConfigureAwait(true);
            await LoadEntitlementNoteAsync().ConfigureAwait(true);
            if (catalogLoaded && !StatusIsError)
            {
                SetStatus(string.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh hosting page");
            SetStatus(ex.Message, true);
        }
        finally
        {
            IsBusy = false;
            ProgressLabel = string.Empty;
        }
    }

    [RelayCommand]
    private async Task InstallSelectedVersionAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedCatalogVersion))
        {
            SetStatus("Pick a version to install.", true);
            return;
        }

        var catalog = await _catalog.GetVersionsAsync().ConfigureAwait(true);
        if (!catalog.IsSuccess)
        {
            SetStatus(catalog.Error ?? "Could not load version catalog.", true);
            return;
        }

        var version = catalog.Value!.FirstOrDefault(v =>
            string.Equals(v.Version, SelectedCatalogVersion, StringComparison.OrdinalIgnoreCase));
        if (version is null)
        {
            SetStatus("Selected version is not in the catalog.", true);
            return;
        }

        await InstallVersionAsync(version).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StartServerAsync()
    {
        if (!CanStart || string.IsNullOrWhiteSpace(SelectedInstalledVersion))
        {
            return;
        }

        IsBusy = true;
        Progress = 0;
        ProgressLabel = "Preparing to start server...";
        SetStatus(string.Empty);

        try
        {
            var result = await _serverHost.StartAsync(new GameServerStartRequest
            {
                InstallsRoot = ResolveInstallsRoot(),
                Version = SelectedInstalledVersion,
                ServerDataPath = ResolveServerDataPath(),
                Progress = new Progress<double>(value =>
                {
                    Progress = value;
                    ProgressLabel = value >= 1.0
                        ? "Launching server process..."
                        : $"Downloading .NET runtime... {value:P0}";
                }),
            }).ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                SetStatus(result.Error ?? "Could not start the server.", true);
            }
            else
            {
                SetStatus($"Vintage Story server {SelectedInstalledVersion} is running.");
            }
        }
        finally
        {
            IsBusy = false;
            Progress = 0;
            ProgressLabel = string.Empty;
        }
    }

    [RelayCommand]
    private async Task StopServerAsync()
    {
        if (!CanStop)
        {
            return;
        }

        IsBusy = true;
        ProgressLabel = "Stopping server...";
        SetStatus(string.Empty);
        try
        {
            var result = await _serverHost.StopAsync().ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                SetStatus(result.Error ?? "Could not stop the server.", true);
            }
            else
            {
                SetStatus("Server stopped.");
            }
        }
        finally
        {
            IsBusy = false;
            ProgressLabel = string.Empty;
        }
    }

    [RelayCommand]
    private async Task RestartServerAsync()
    {
        if (!CanRestart)
        {
            return;
        }

        IsBusy = true;
        ProgressLabel = "Restarting server...";
        SetStatus(string.Empty);
        try
        {
            var result = await _serverHost.RestartAsync().ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                SetStatus(result.Error ?? "Could not restart the server.", true);
            }
            else
            {
                SetStatus($"Vintage Story server {SelectedInstalledVersion} restarted.");
            }
        }
        finally
        {
            IsBusy = false;
            ProgressLabel = string.Empty;
        }
    }

    [RelayCommand]
    private async Task BrowseServerDataPathAsync()
    {
        var path = await _storagePicker.PickFolderAsync("Select server data folder").ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            _settings.ServerDataPath = path;
            UpdateFolderRows();
            await PersistSettingsAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void OpenWiki()
    {
        _urlLauncher.OpenUrl("https://wiki.vintagestory.at/Setting_up_a_Multiplayer_Server");
    }

    private async Task LoadInstalledVersionsAsync()
    {
        var installsRoot = ResolveInstallsRoot();
        Directory.CreateDirectory(installsRoot);
        var list = await _installedStore.ListAsync(installsRoot).ConfigureAwait(true);
        if (!list.IsSuccess)
        {
            SetStatus(list.Error ?? "Could not list installed server versions.", true);
            return;
        }

        InstalledServerVersions.Clear();
        foreach (var item in list.Value!.OrderByDescending(v => v.Version, Comparer<string>.Create(GameVersionComparer.Compare)))
        {
            InstalledServerVersions.Add(item.Version);
        }

        if (InstalledServerVersions.Count == 0)
        {
            SelectedInstalledVersion = null;
        }
        else if (string.IsNullOrWhiteSpace(SelectedInstalledVersion)
            || !InstalledServerVersions.Contains(SelectedInstalledVersion))
        {
            SelectedInstalledVersion = InstalledServerVersions[0];
        }

        UpdateFolderRows();
        NotifyCommandState();
    }

    private async Task<bool> LoadCatalogVersionsAsync()
    {
        var catalog = await _catalog.GetVersionsAsync().ConfigureAwait(true);
        if (!catalog.IsSuccess)
        {
            LatestStableServerVersion = string.Empty;
            SetStatus(catalog.Error ?? "Could not load the version catalog.", true);
            OnPropertyChanged(nameof(HasInstallableServerVersions));
            OnPropertyChanged(nameof(ShowCatalogVersionPicker));
            OnPropertyChanged(nameof(CanInstallLatestServer));
            OnPropertyChanged(nameof(InstallLatestLabel));
            return false;
        }

        await UpdateLatestStableServerVersionAsync(catalog.Value!).ConfigureAwait(true);

        var platform = _platform.GetPlatformInfo();
        var installed = InstalledServerVersions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        CatalogServerVersions.Clear();
        foreach (var version in catalog.Value!
                     .Where(v => _installer.SelectServerPackage(v, platform) is not null)
                     .OrderByDescending(v => v.Version, Comparer<string>.Create(GameVersionComparer.Compare)))
        {
            if (!installed.Contains(version.Version))
            {
                CatalogServerVersions.Add(version.Version);
            }
        }

        if (CatalogServerVersions.Count > 0
            && (string.IsNullOrWhiteSpace(SelectedCatalogVersion)
                || !CatalogServerVersions.Contains(SelectedCatalogVersion)))
        {
            SelectedCatalogVersion = CatalogServerVersions[0];
        }
        else if (CatalogServerVersions.Count == 0)
        {
            SelectedCatalogVersion = null;
        }

        OnPropertyChanged(nameof(HasInstallableServerVersions));
        OnPropertyChanged(nameof(ShowCatalogVersionPicker));
        OnPropertyChanged(nameof(CanInstallLatestServer));
        OnPropertyChanged(nameof(InstallLatestLabel));
        return true;
    }

    private async Task UpdateLatestStableServerVersionAsync(IReadOnlyList<GameVersionInfo> catalogVersions)
    {
        var platform = _platform.GetPlatformInfo();
        var latestResult = await _catalog.GetLatestStableVersionAsync().ConfigureAwait(true);
        if (latestResult.IsSuccess && !string.IsNullOrWhiteSpace(latestResult.Value))
        {
            var official = catalogVersions.FirstOrDefault(v =>
                string.Equals(v.Version, latestResult.Value, StringComparison.OrdinalIgnoreCase)
                && _installer.SelectServerPackage(v, platform) is not null);
            if (official is not null)
            {
                LatestStableServerVersion = official.Version;
                return;
            }
        }

        var latest = catalogVersions
            .Where(v => v.Channel is GameVersionChannel.Stable)
            .Where(v => _installer.SelectServerPackage(v, platform) is not null)
            .OrderByDescending(v => v.Version, Comparer<string>.Create(GameVersionComparer.Compare))
            .FirstOrDefault();

        LatestStableServerVersion = latest?.Version ?? string.Empty;
    }

    private async Task LoadEntitlementNoteAsync()
    {
        ShowEntitlementNote = false;
        EntitlementNote = string.Empty;

        var status = await _accountAuth.GetStatusAsync().ConfigureAwait(true);
        if (!status.IsSuccess || !status.Value!.IsSignedIn)
        {
            return;
        }

        var hostGameServer = status.Value.HostGameServer;
        if (string.IsNullOrWhiteSpace(hostGameServer)
            || string.Equals(hostGameServer, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(hostGameServer, "1", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ShowEntitlementNote = true;
        EntitlementNote = "Your Vintage Story account may not include a dedicated server entitlement. You can still run a local server if you have the server files.";
    }

    private async Task InstallVersionAsync(GameVersionInfo version, bool isUpgrade = false)
    {
        _installCts?.Cancel();
        _installCts = new CancellationTokenSource();
        var token = _installCts.Token;
        var installsRoot = ResolveInstallsRoot();
        Directory.CreateDirectory(installsRoot);
        _settings.InstallsRoot = installsRoot;

        var actionLabel = isUpgrade ? "Upgrading" : "Installing";
        var session = _transfers.Begin(
            $"server-{version.Version}-{Guid.NewGuid():N}",
            isUpgrade ? $"Upgrade server {version.Version}" : $"Server {version.Version}",
            TransferJobKind.Version);

        IsBusy = true;
        Progress = 0;
        ProgressLabel = $"{actionLabel} server {version.Version}...";
        SetStatus(string.Empty);

        try
        {
            await session.StartAsync(token).ConfigureAwait(true);
            var result = await RunServerInstallAsync(version, installsRoot, session, token, actionLabel).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                return;
            }

            var runtimeReady = await EnsureRuntimeForVersionAsync(version.Version, token).ConfigureAwait(true);
            if (!runtimeReady)
            {
                SetStatus(
                    $"Server {version.Version} is installed, but the required .NET runtime could not be prepared. Start may fail until the runtime downloads.",
                    isError: true);
            }

            SelectedInstalledVersion = version.Version;
            _settings.SelectedServerVersion = version.Version;
            await PersistSettingsAsync().ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            if (runtimeReady)
            {
                SetStatus(isUpgrade
                    ? $"Upgraded to server {version.Version}."
                    : $"Installed server {version.Version}.");
            }

            Progress = 1;
        }
        catch (OperationCanceledException)
        {
            session.Cancel();
            SetStatus($"{actionLabel} server {version.Version} canceled.");
        }
        finally
        {
            IsBusy = false;
            ProgressLabel = string.Empty;
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<Result> RunServerInstallAsync(
        GameVersionInfo version,
        string installsRoot,
        ITransferSession session,
        CancellationToken token,
        string actionLabel)
    {
        Progress = 0;
        ProgressLabel = $"Downloading server {version.Version}...";
        SetStatus(string.Empty);

        var progress = new Progress<double>(value =>
        {
            Progress = value;
            session.Report(value);
            ProgressLabel = value >= 0.9
                ? $"{actionLabel} server {version.Version}..."
                : $"Downloading server {version.Version}... {value:P0}";
        });

        var result = await _installer.InstallAsync(new ServerInstallRequest
        {
            InstallsRoot = installsRoot,
            Version = version,
            Progress = progress,
        }, token).ConfigureAwait(true);

        if (!result.IsSuccess)
        {
            session.Fail(result.Error ?? "Install failed.");
            SetStatus(result.Error ?? "Install failed.", true);
            return Result.Failure(result.Error ?? "Install failed.");
        }

        session.Complete($"Installed server {version.Version}");
        return Result.Success();
    }

    private async Task<bool> EnsureRuntimeForVersionAsync(string version, CancellationToken cancellationToken)
    {
        var major = GameDotNetRuntimeRequirements.TryGetRequiredMajor(version);
        if (!major.IsSuccess)
        {
            return true;
        }

        var session = _transfers.Begin(
            $"runtime-server-{version}-{Guid.NewGuid():N}",
            $".NET {major.Value} runtime",
            TransferJobKind.Runtime);

        try
        {
            await session.StartAsync(cancellationToken).ConfigureAwait(true);
            ProgressLabel = $"Preparing .NET {major.Value} runtime...";
            var progress = new Progress<double>(value =>
            {
                session.Report(value);
                Progress = value;
                ProgressLabel = value >= 1.0
                    ? $".NET {major.Value} runtime ready"
                    : $"Downloading .NET {major.Value} runtime... {value:P0}";
            });

            var runtime = await _runtimeProvisioner.EnsureAsync(major.Value, progress, cancellationToken)
                .ConfigureAwait(true);
            if (!runtime.IsSuccess)
            {
                session.Fail(runtime.Error ?? "Runtime provision failed.");
                _logger.LogWarning("Server runtime provision failed for {Version}: {Error}", version, runtime.Error);
                return false;
            }

            session.Complete($".NET {major.Value} runtime ready");
            return true;
        }
        catch (OperationCanceledException)
        {
            session.Cancel();
            throw;
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }
}
