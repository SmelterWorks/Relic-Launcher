using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Versions;

namespace RelicLauncher.App.ViewModels;

public partial class HostingViewModel
{
    [ObservableProperty]
    private string _latestStableServerVersion = string.Empty;

    public bool HasUpgradeAvailable =>
        !string.IsNullOrWhiteSpace(SelectedInstalledVersion)
        && !string.IsNullOrWhiteSpace(LatestStableServerVersion)
        && GameVersionComparer.Compare(SelectedInstalledVersion, LatestStableServerVersion) < 0;

    public bool CanUninstallSelected =>
        IsLocalHostingSupported
        && !IsBusy
        && ServerState is ServerProcessState.Stopped
        && !string.IsNullOrWhiteSpace(SelectedInstalledVersion);

    public bool CanUpgradeToLatest =>
        IsLocalHostingSupported
        && !IsBusy
        && ServerState is ServerProcessState.Stopped
        && HasUpgradeAvailable;

    public string UpgradeTooltip => string.IsNullOrWhiteSpace(LatestStableServerVersion)
        ? "Install the latest server version"
        : $"Upgrade to server {LatestStableServerVersion}";

    [RelayCommand]
    private async Task UninstallSelectedVersionAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedInstalledVersion))
        {
            return;
        }

        if (ServerState is not ServerProcessState.Stopped)
        {
            SetStatus("Stop the server before uninstalling a version.", true);
            return;
        }

        var version = SelectedInstalledVersion;
        var confirmed = await _confirmDialog.ConfirmAsync(
            "Uninstall server",
            $"Uninstall Vintage Story server {version}? This removes the managed server files.",
            "Uninstall",
            "Cancel").ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        ProgressLabel = $"Removing server {version}...";
        SetStatus(string.Empty);
        try
        {
            var result = await _installer.UninstallAsync(ResolveInstallsRoot(), version).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                SetStatus(result.Error ?? "Could not uninstall server version.", true);
                return;
            }

            if (string.Equals(_settings.SelectedServerVersion, version, StringComparison.OrdinalIgnoreCase))
            {
                _settings.SelectedServerVersion = null;
                await PersistSettingsAsync().ConfigureAwait(true);
            }

            await RefreshAsync().ConfigureAwait(true);
            SetStatus($"Removed server {version}.");
        }
        finally
        {
            IsBusy = false;
            ProgressLabel = string.Empty;
        }
    }

    [RelayCommand]
    private async Task UpgradeToLatestAsync()
    {
        if (!HasUpgradeAvailable)
        {
            return;
        }

        if (ServerState is not ServerProcessState.Stopped)
        {
            SetStatus("Stop the server before upgrading.", true);
            return;
        }

        var latest = await ResolveLatestServerVersionInfoAsync().ConfigureAwait(true);
        if (latest is null)
        {
            SetStatus("Could not find the latest server version in the catalog.", true);
            return;
        }

        await InstallVersionAsync(latest, isUpgrade: true).ConfigureAwait(true);
    }

    private async Task<GameVersionInfo?> ResolveLatestServerVersionInfoAsync()
    {
        if (string.IsNullOrWhiteSpace(LatestStableServerVersion))
        {
            return null;
        }

        var catalog = await _catalog.GetVersionsAsync().ConfigureAwait(true);
        if (!catalog.IsSuccess)
        {
            return null;
        }

        var platform = _platform.GetPlatformInfo();
        return catalog.Value!.FirstOrDefault(v =>
            string.Equals(v.Version, LatestStableServerVersion, StringComparison.OrdinalIgnoreCase)
            && _installer.SelectServerPackage(v, platform) is not null);
    }

    public bool HasInstallableServerVersions =>
        ShowCatalogVersionPicker || CanInstallLatestServer;

    public bool ShowCatalogVersionPicker => CatalogServerVersions.Count > 0;

    public bool CanInstallLatestServer =>
        IsLocalHostingSupported
        && !IsBusy
        && ServerState is ServerProcessState.Stopped
        && !string.IsNullOrWhiteSpace(LatestStableServerVersion)
        && !InstalledServerVersions.Any(v => string.Equals(v, LatestStableServerVersion, StringComparison.OrdinalIgnoreCase));

    public string InstallLatestLabel =>
        string.IsNullOrWhiteSpace(LatestStableServerVersion)
            ? "Install latest"
            : $"Install {LatestStableServerVersion}";

    [RelayCommand]
    private async Task InstallLatestServerAsync()
    {
        if (!CanInstallLatestServer)
        {
            return;
        }

        var latest = await ResolveLatestServerVersionInfoAsync().ConfigureAwait(true);
        if (latest is null)
        {
            SetStatus("Could not find the latest server version in the catalog.", true);
            return;
        }

        await InstallVersionAsync(latest).ConfigureAwait(true);
    }

    partial void OnLatestStableServerVersionChanged(string value)
    {
        NotifyCommandState();
        OnPropertyChanged(nameof(HasInstallableServerVersions));
        OnPropertyChanged(nameof(ShowCatalogVersionPicker));
        OnPropertyChanged(nameof(CanInstallLatestServer));
        OnPropertyChanged(nameof(InstallLatestLabel));
    }
}
