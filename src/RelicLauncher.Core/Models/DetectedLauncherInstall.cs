namespace RelicLauncher.Core.Models;

public sealed class DetectedLauncherInstall
{
    public required LauncherInstallKind InstallKind { get; init; }
    public required string Rid { get; init; }
    public string? InstallDirectory { get; init; }
    public string? ExecutablePath { get; init; }
    public bool CanApplyInApp { get; init; }
}
