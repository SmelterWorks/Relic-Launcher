namespace RelicLauncher.Core.Models;

public sealed class ModpackApplySummary
{
    public int InstalledCount { get; init; }
    public int UpdatedCount { get; init; }
    public int RemovedCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
