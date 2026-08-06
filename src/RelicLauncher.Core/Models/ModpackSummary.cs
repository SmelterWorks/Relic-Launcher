namespace RelicLauncher.Core.Models;

public sealed class ModpackSummary
{
    public required string Path { get; init; }
    public required ModpackManifest Manifest { get; init; }
    public int ModCount { get; init; }
    public long TotalBytes { get; init; }
}
