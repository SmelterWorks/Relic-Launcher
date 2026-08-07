using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.Services;

public sealed class LauncherUpdateStartupService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly LauncherUpdateCoordinator _coordinator;
    private readonly ILogger<LauncherUpdateStartupService> _logger;

    public LauncherUpdateStartupService(
        LauncherUpdateCoordinator coordinator,
        ILogger<LauncherUpdateStartupService> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    public void ScheduleThrottledCheck(LauncherSettings settings, Action<LauncherSettings> onSettingsChanged)
    {
        if (settings.LauncherUpdateMode == LauncherUpdateMode.Off)
        {
            return;
        }

        if (settings.LastLauncherUpdateCheckUtc is { } lastCheck &&
            DateTimeOffset.UtcNow - lastCheck < CheckInterval)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _coordinator.CheckAndPromptAsync(settings, onSettingsChanged, force: false).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Startup launcher update check failed");
            }
        });
    }
}
