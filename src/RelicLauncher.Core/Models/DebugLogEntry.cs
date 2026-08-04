namespace RelicLauncher.Core.Models;

public sealed class DebugLogEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
    public string? Source { get; init; }
    public string? Exception { get; init; }
}
