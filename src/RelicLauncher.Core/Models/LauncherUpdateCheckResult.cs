namespace RelicLauncher.Core.Models;

public sealed class LauncherUpdateCheckResult
{
    public LauncherUpdateInfo? Update { get; init; }
    public string? Etag { get; init; }
    public bool NotModified { get; init; }
}
