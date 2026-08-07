namespace RelicLauncher.Core.Sandbox;

public sealed class SandboxPolicy
{
    public SandboxKind Kind { get; init; }

    public IList<PathGrant> PathGrants { get; init; } = [];

    public IList<NetPortGrant> NetPortGrants { get; init; } = [];

    public bool ScopeAbstractUnixSocket { get; init; }

    public bool ScopeSignal { get; init; }

    public SeccompProfile SeccompProfile { get; init; } = SeccompProfile.Default;

    public int MaxLandlockAbi { get; init; } = 9;
}
