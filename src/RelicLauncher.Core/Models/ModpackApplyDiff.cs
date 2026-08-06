namespace RelicLauncher.Core.Models;

public sealed class ModpackApplyDiff
{
    public IReadOnlyList<ModpackApplyDiffEntry> Entries { get; init; } = [];
}
