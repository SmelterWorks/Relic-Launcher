using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Security;
using RelicLauncher.Core.Versions;

namespace RelicLauncher.App.ViewModels;

public partial class ServersViewModel
{
    [RelayCommand(CanExecute = nameof(CanExecuteJoin))]
    private async Task JoinSelectedAsync()
    {
        if (SelectedBrowseServer is null)
        {
            return;
        }

        await JoinAddressAsync(SelectedBrowseServer.Address, null).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task JoinTopSAsync() => await JoinAddressAsync(TopSAddress, null).ConfigureAwait(true);

    [RelayCommand]
    private async Task DirectConnectAsync()
    {
        DirectValidationError = string.Empty;
        if (!ConnectAddressValidator.TryNormalize(DirectAddress, out var normalized, out var error))
        {
            DirectValidationError = error ?? ConnectAddressValidator.InvalidAddressMessage;
            return;
        }

        await JoinAddressAsync(normalized, DirectPassword).ConfigureAwait(true);
        DirectPassword = string.Empty;
    }

    [RelayCommand]
    private async Task JoinLocalhostAsync() =>
        await JoinAddressAsync("127.0.0.1:42420", null).ConfigureAwait(true);

    [RelayCommand]
    private async Task JoinLanAddressAsync(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        LanValidationError = string.Empty;
        if (!ConnectAddressValidator.TryNormalize(address, out var normalized, out var error))
        {
            LanValidationError = error ?? ConnectAddressValidator.InvalidAddressMessage;
            return;
        }

        await JoinAddressAsync(normalized, null).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task JoinLanManualAsync()
    {
        LanValidationError = string.Empty;
        if (!ConnectAddressValidator.TryNormalize(LanManualAddress, out var normalized, out var error))
        {
            LanValidationError = error ?? ConnectAddressValidator.InvalidAddressMessage;
            return;
        }

        await JoinAddressAsync(normalized, null).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task JoinRecentAsync(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        if (!ConnectAddressValidator.TryNormalize(address, out var normalized, out _))
        {
            return;
        }

        await JoinAddressAsync(normalized, null).ConfigureAwait(true);
    }

    private bool CanExecuteJoin() => CanJoin && !IsJoining;

    private async Task JoinAddressAsync(string address, string? password)
    {
        IsJoining = true;
        SetStatus("Launching game client...");
        var version = _settings.SelectedVersion ?? string.Empty;
        var runtimeMajor = GameDotNetRuntimeRequirements.TryGetRequiredMajor(version);
        var runtimeLabel = runtimeMajor.IsSuccess
            ? $".NET {runtimeMajor.Value} runtime"
            : ".NET runtime";
        var session = _transfers.Begin(
            $"runtime-join-{version}-{Guid.NewGuid():N}",
            runtimeLabel,
            TransferJobKind.Runtime);

        try
        {
            await session.StartAsync().ConfigureAwait(true);
            var installsRoot = _settings.InstallsRoot ?? _platform.GetPlatformInfo().DefaultInstallsRoot;
            var progress = new Progress<double>(value =>
            {
                session.Report(value);
                SetStatus(value < 1.0
                    ? $"Preparing {runtimeLabel}... {value:P0}"
                    : "Connecting...");
            });

            var result = await _launchService.LaunchAsync(new GameLaunchRequest
            {
                InstallsRoot = installsRoot,
                Version = version,
                DataPath = _settings.DataPath,
                ConnectAddress = address,
                ConnectPassword = password,
                Progress = progress,
            }).ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                session.Fail(result.Error ?? "Launch failed.");
                SetStatus(result.Error ?? "Launch failed.", true);
                _logger.LogWarning("Server join failed: {Error}", result.Error);
                await RefreshJoinStateAsync().ConfigureAwait(true);
                return;
            }

            session.Complete($"Connected to {address}");
            SetStatus($"Launched and connecting to {address}.");
            await _recents.RecordAsync(address).ConfigureAwait(true);
            await LoadRecentsAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            session.Cancel();
            SetStatus("Launch canceled.");
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
            IsJoining = false;
        }
    }

    private async Task RefreshJoinStateAsync()
    {
        var status = await _accountAuth.GetStatusAsync().ConfigureAwait(true);
        IsSignedIn = status.IsSuccess && status.Value?.IsSignedIn == true;
        HasInstalledVersion = !string.IsNullOrWhiteSpace(_settings.SelectedVersion);
        CanJoin = IsSignedIn && HasInstalledVersion;
        if (!IsSignedIn)
        {
            VersionMismatchWarning = string.Empty;
            return;
        }

        if (!HasInstalledVersion)
        {
            VersionMismatchWarning = "Install a game version on the Versions page first.";
            return;
        }

        UpdateSelectedDetail();
    }
}
