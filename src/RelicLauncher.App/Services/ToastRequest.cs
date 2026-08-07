namespace RelicLauncher.App.Services;

public sealed class ToastRequest
{
    public string? Title { get; init; }
    public required string Message { get; init; }
    public ToastSeverity Severity { get; init; } = ToastSeverity.Info;
    public TimeSpan? Duration { get; init; }
    public IReadOnlyList<ToastAction>? Actions { get; init; }
    public string? ProgressText { get; init; }
}
