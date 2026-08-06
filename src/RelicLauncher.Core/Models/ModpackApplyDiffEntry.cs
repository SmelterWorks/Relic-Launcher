namespace RelicLauncher.Core.Models;

public sealed class ModpackApplyDiffEntry
{
    public required string ModId { get; init; }
    public string? CurrentVersion { get; init; }
    public string? PackVersion { get; init; }
    public ModpackApplyDiffKind Kind { get; init; }
}
