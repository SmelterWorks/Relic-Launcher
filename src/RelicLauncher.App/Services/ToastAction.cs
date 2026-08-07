namespace RelicLauncher.App.Services;

public sealed class ToastAction
{
    public required string Label { get; init; }
    public required Func<Task> Handler { get; init; }
    public bool DismissOnClick { get; init; } = true;
}
