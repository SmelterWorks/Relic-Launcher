namespace RelicLauncher.Core.Models;

public sealed class LauncherUpdateAsset
{
    public required string InstallKind { get; init; }
    public required string Rid { get; init; }
    public required string Filename { get; init; }
    public required string Url { get; init; }
    public required string Sha256 { get; init; }
    public long SizeBytes { get; init; }
}
