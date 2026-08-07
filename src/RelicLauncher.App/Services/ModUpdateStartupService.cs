using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.Services;

public sealed class ModUpdateStartupService
{
    private readonly IModUpdateCheckService _updateCheck;
    private readonly IRuntimePlatform _platform;
    private readonly ILogger<ModUpdateStartupService> _logger;

    public ModUpdateStartupService(
        IModUpdateCheckService updateCheck,
        IRuntimePlatform platform,
        ILogger<ModUpdateStartupService> logger)
    {
        _updateCheck = updateCheck;
        _platform = platform;
        _logger = logger;
    }

    public void ScheduleThrottledCheck(LauncherSettings settings)
    {
        if (settings.ModUpdateMode == ModUpdateMode.Off)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.SelectedVersion))
        {
            return;
        }

        var dataPath = string.IsNullOrWhiteSpace(settings.DataPath)
            ? _platform.GetPlatformInfo().DefaultDataPath
            : settings.DataPath.Trim();

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _updateCheck.CheckForUpdatesAsync(
                    dataPath,
                    settings.SelectedVersion!,
                    settings.ModUpdateOptOutModIds,
                    force: false).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    _logger.LogDebug("Startup mod update check failed: {Error}", result.Error);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Startup mod update check failed");
            }
        });
    }
}
