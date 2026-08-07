using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.Services;

public sealed partial class LauncherUpdateCoordinator
{
    private readonly IUpdateCheckService _updateCheck;
    private readonly IInstallKindDetector _installKindDetector;
    private readonly ILauncherUpdateAssetSelector _assetSelector;
    private readonly ILauncherUpdateApplyService _applyService;
    private readonly IToastService _toastService;
    private readonly IConfirmDialogService _confirmDialog;
    private readonly IUrlLauncher _urlLauncher;
    private readonly ILauncherSettingsStore _settingsStore;
    private readonly ILogger<LauncherUpdateCoordinator> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LauncherUpdateCoordinator(
        IUpdateCheckService updateCheck,
        IInstallKindDetector installKindDetector,
        ILauncherUpdateAssetSelector assetSelector,
        ILauncherUpdateApplyService applyService,
        IToastService toastService,
        IConfirmDialogService confirmDialog,
        IUrlLauncher urlLauncher,
        ILauncherSettingsStore settingsStore,
        ILogger<LauncherUpdateCoordinator> logger)
    {
        _updateCheck = updateCheck;
        _installKindDetector = installKindDetector;
        _assetSelector = assetSelector;
        _applyService = applyService;
        _toastService = toastService;
        _confirmDialog = confirmDialog;
        _urlLauncher = urlLauncher;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public async Task<LauncherUpdateCheckOutcome> CheckAndPromptAsync(
        LauncherSettings settings,
        Action<LauncherSettings>? onSettingsChanged,
        bool force)
    {
        if (settings.LauncherUpdateMode == LauncherUpdateMode.Off)
        {
            return LauncherUpdateCheckOutcome.Skipped;
        }

        if (!await _gate.WaitAsync(0).ConfigureAwait(false))
        {
            return LauncherUpdateCheckOutcome.Busy;
        }

        try
        {
            return await CheckCoreAsync(settings, onSettingsChanged, force).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<LauncherUpdateCheckOutcome> CheckCoreAsync(
        LauncherSettings settings,
        Action<LauncherSettings>? onSettingsChanged,
        bool force)
    {
        var request = new LauncherUpdateCheckRequest
        {
            Channel = settings.LauncherUpdateChannel,
            IfNoneMatchEtag = force ? null : settings.LastUpdateManifestEtag,
        };

        var result = await _updateCheck.CheckForLauncherUpdateAsync(request).ConfigureAwait(false);
        settings.LastLauncherUpdateCheckUtc = DateTimeOffset.UtcNow;

        if (!result.IsSuccess)
        {
            _logger.LogDebug("Launcher update check failed: {Error}", result.Error);
            await SaveSettingsAsync(settings, onSettingsChanged).ConfigureAwait(false);
            return LauncherUpdateCheckOutcome.Failed;
        }

        if (!string.IsNullOrWhiteSpace(result.Value!.Etag))
        {
            settings.LastUpdateManifestEtag = result.Value.Etag;
        }

        if (result.Value.NotModified || result.Value.Update is null)
        {
            await SaveSettingsAsync(settings, onSettingsChanged).ConfigureAwait(false);
            return LauncherUpdateCheckOutcome.UpToDate;
        }

        var update = result.Value.Update;
        if (string.Equals(settings.DismissedLauncherUpdateVersion, update.Version, StringComparison.OrdinalIgnoreCase))
        {
            await SaveSettingsAsync(settings, onSettingsChanged).ConfigureAwait(false);
            return LauncherUpdateCheckOutcome.Dismissed;
        }

        await SaveSettingsAsync(settings, onSettingsChanged).ConfigureAwait(false);
        await RunOnUiAsync(() => ShowUpdateToastAsync(settings, onSettingsChanged, update)).ConfigureAwait(false);
        return LauncherUpdateCheckOutcome.UpdateAvailable;
    }

    private Task ShowUpdateToastAsync(
        LauncherSettings settings,
        Action<LauncherSettings>? onSettingsChanged,
        LauncherUpdateInfo update)
    {
        _toastService.Show(new ToastRequest
        {
            Title = "Update available",
            Message = $"Relic Launcher {update.Version} is available.",
            Severity = ToastSeverity.Info,
            Actions =
            [
                new ToastAction
                {
                    Label = "Update now",
                    Handler = () => BeginUpdateAsync(settings, onSettingsChanged, update),
                },
                new ToastAction
                {
                    Label = "Later",
                    Handler = () =>
                    {
                        settings.DismissedLauncherUpdateVersion = update.Version;
                        return SaveSettingsAsync(settings, onSettingsChanged);
                    },
                },
            ],
        });

        return Task.CompletedTask;
    }

    private async Task SaveSettingsAsync(LauncherSettings settings, Action<LauncherSettings>? onSettingsChanged)
    {
        var save = await _settingsStore.SaveAsync(settings).ConfigureAwait(false);
        if (save.IsSuccess)
        {
            onSettingsChanged?.Invoke(settings);
        }
    }

    private static async Task RunOnUiAsync(Func<Task> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            await action().ConfigureAwait(true);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action).ConfigureAwait(true);
    }
}
