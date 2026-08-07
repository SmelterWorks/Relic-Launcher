using RelicLauncher.Core.Abstractions;

namespace RelicLauncher.Testing;

public sealed class TestSandboxSupport : ISandboxSupport
{
    public bool IsIsolationAvailable => false;

    public bool IsRunningSandboxed => false;

    public bool IsBrokerConnected => false;

    public int? LandlockAbi => null;

    public bool SeccompAvailable => false;

    public bool AppContainerAvailable => false;

    public string GetStatusSummary() => "Disabled in tests";

    public SandboxIsolationStatus GetStatus() => new();
}
