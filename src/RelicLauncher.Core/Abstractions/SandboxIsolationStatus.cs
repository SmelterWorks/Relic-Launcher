namespace RelicLauncher.Core.Abstractions;

public sealed class SandboxIsolationStatus
{
    public bool LauncherSandboxed { get; init; }

    public bool ClientLaunchSandboxed { get; init; }

    public bool ServerLaunchSandboxed { get; init; }

    public string? ClientDegradedReason { get; init; }

    public int? LandlockAbi { get; init; }

    public bool SeccompActive { get; init; }

    public bool AppContainerActive { get; init; }
}
