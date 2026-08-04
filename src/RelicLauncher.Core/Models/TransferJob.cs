namespace RelicLauncher.Core.Models;

public sealed class TransferJob
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public TransferJobKind Kind { get; init; }
    public TransferJobState State { get; set; } = TransferJobState.Queued;
    public double Progress { get; set; }
    public string? StatusText { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
}
