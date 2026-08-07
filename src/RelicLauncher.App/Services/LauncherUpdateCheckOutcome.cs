namespace RelicLauncher.App.Services;

public enum LauncherUpdateCheckOutcome
{
    Skipped,
    Busy,
    Failed,
    UpToDate,
    Dismissed,
    UpdateAvailable,
}
