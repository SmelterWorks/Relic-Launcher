namespace RelicLauncher.Core.Sandbox;

public sealed class SandboxLaunchResult
{
    public int ProcessId { get; init; }

    public bool Sandboxed { get; init; }

    public string? DegradedReason { get; init; }
}
