namespace RelicLauncher.Core.Abstractions;

public interface ISandboxSupport
{
    bool IsIsolationAvailable { get; }

    bool IsRunningSandboxed { get; }

    bool IsBrokerConnected { get; }

    int? LandlockAbi { get; }

    bool SeccompAvailable { get; }

    bool AppContainerAvailable { get; }

    string GetStatusSummary();

    SandboxIsolationStatus GetStatus();
}
