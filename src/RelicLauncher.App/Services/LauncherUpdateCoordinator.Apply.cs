using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.Services;

public sealed partial class LauncherUpdateCoordinator
{
    private async Task BeginUpdateAsync(
        LauncherSettings settings,
        Action<LauncherSettings>? onSettingsChanged,
        LauncherUpdateInfo update)
    {
        var install = _installKindDetector.Detect();
        var asset = _assetSelector.Select(update, install);
        if (asset is null || !_applyService.CanApplyInApp(install.InstallKind) || !install.CanApplyInApp)
        {
            ShowManualUpdateToast();
            return;
        }

        var confirmed = await _confirmDialog.ConfirmAsync(
            "Install update",
            $"Relic Launcher will download version {update.Version} and restart to finish installing. Continue?",
            "Install",
            "Cancel").ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        var toastId = await ShowDownloadToastAsync(asset.Filename).ConfigureAwait(false);
        var progress = CreateDownloadProgress(toastId);
        var apply = await _applyService.DownloadAndApplyAsync(asset, install, progress).ConfigureAwait(false);
        if (toastId is not null)
        {
            _toastService.Dismiss(toastId.Value);
        }

        if (!apply.IsSuccess)
        {
            await ShowUpdateFailedToastAsync(apply.Error).ConfigureAwait(false);
        }
    }

    private void ShowManualUpdateToast()
    {
        _toastService.Show(new ToastRequest
        {
            Title = "Manual update required",
            Message = "Download the latest build for your install type from the Relic download page.",
            Severity = ToastSeverity.Warning,
            Actions =
            [
                new ToastAction
                {
                    Label = "Open download page",
                    Handler = () =>
                    {
                        _urlLauncher.OpenUrl(RelicLauncherEndpoints.DownloadPageUrl);
                        return Task.CompletedTask;
                    },
                },
            ],
        });
    }

    private async Task<Guid?> ShowDownloadToastAsync(string filename)
    {
        Guid? toastId = null;
        await RunOnUiAsync(() =>
        {
            toastId = _toastService.Show(new ToastRequest
            {
                Title = "Downloading update",
                Message = $"Fetching {filename}",
                Severity = ToastSeverity.Info,
                ProgressText = "Starting download...",
            });
            return Task.CompletedTask;
        }).ConfigureAwait(false);
        return toastId;
    }

    private IProgress<double> CreateDownloadProgress(Guid? toastId)
    {
        return new Progress<double>(value =>
        {
            if (toastId is null)
            {
                return;
            }

            var percent = (int)Math.Round(value * 100);
            _toastService.UpdateProgress(toastId.Value, $"{percent}%");
        });
    }

    private Task ShowUpdateFailedToastAsync(string? error)
    {
        return RunOnUiAsync(() =>
        {
            _toastService.Show(new ToastRequest
            {
                Title = "Update failed",
                Message = error ?? "Could not install the update.",
                Severity = ToastSeverity.Error,
            });
            return Task.CompletedTask;
        });
    }
}
