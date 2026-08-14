namespace RelicLauncher.Core.Models;

public sealed class MigrationJobSummary
{
    public string Uuid { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public long Bytes { get; init; }
}
