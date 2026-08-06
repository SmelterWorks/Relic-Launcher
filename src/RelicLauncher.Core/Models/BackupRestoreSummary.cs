namespace RelicLauncher.Core.Models;

public sealed class BackupRestoreSummary
{
    public bool RestoredMods { get; init; }
    public bool RestoredWorlds { get; init; }
    public IReadOnlyList<string> RestoredVersions { get; init; } = [];
    public int FileCount { get; init; }
}
