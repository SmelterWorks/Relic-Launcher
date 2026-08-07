namespace RelicLauncher.Core.Models;

public sealed class LauncherUpdateCheckRequest
{
    public required LauncherUpdateChannel Channel { get; init; }
    public string? IfNoneMatchEtag { get; init; }
}
