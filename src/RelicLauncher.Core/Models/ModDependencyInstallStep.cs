namespace RelicLauncher.Core.Models;

public sealed class ModDependencyInstallStep
{
    public required string ModId { get; init; }

    public ModReleaseInfo? Release { get; init; }

    public string? RequiredBy { get; init; }

    public int Depth { get; init; }

    public string? MinimumVersion { get; init; }

    public bool IsUnresolved { get; init; }

    public string? Error { get; init; }
}
