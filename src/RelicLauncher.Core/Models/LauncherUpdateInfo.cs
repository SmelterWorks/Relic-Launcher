namespace RelicLauncher.Core.Models;

public sealed class LauncherUpdateInfo
{
    public required string Version { get; init; }
    public required string ReleaseNotesUrl { get; init; }
    public required LauncherUpdateChannel Channel { get; init; }
    public required IReadOnlyList<LauncherUpdateAsset> Assets { get; init; }
}
