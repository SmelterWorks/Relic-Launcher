namespace RelicLauncher.Core.Sandbox;

public sealed class SandboxLaunchRequest
{
    public required SandboxKind Kind { get; init; }

    public required string ExecutablePath { get; init; }

    public IList<string> Arguments { get; init; } = [];

    public IDictionary<string, string?> Environment { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);

    public string? WorkingDirectory { get; init; }

    public bool RedirectStandardInput { get; init; }

    public bool RedirectStandardOutput { get; init; }

    public bool RedirectStandardError { get; init; }

    public SandboxPolicy? PolicyOverride { get; init; }
}
