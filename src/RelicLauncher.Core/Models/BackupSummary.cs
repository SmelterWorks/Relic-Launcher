namespace RelicLauncher.Core.Models;

public sealed class BackupSummary
{
    public required string ZipPath { get; init; }
    public bool IncludedMods { get; init; }
    public bool IncludedWorlds { get; init; }
    public IReadOnlyList<string> IncludedVersions { get; init; } = [];
    public long TotalBytes { get; init; }
    public int FileCount { get; init; }
}
