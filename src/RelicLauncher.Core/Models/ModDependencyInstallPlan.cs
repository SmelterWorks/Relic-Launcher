namespace RelicLauncher.Core.Models;

public sealed class ModDependencyInstallPlan
{
    public required ModReleaseInfo RootRelease { get; init; }

    public string? RootModId { get; init; }

    public IReadOnlyList<ModDependencyInstallStep> Steps { get; init; } = [];

    public IReadOnlyList<ModDependencyInstallStep> Unresolved { get; init; } = [];

    public IReadOnlyList<ModDependencyInstallStep> ReleasesToInstall
        => Steps.Where(s => !s.IsUnresolved && s.Release is not null).ToList();
}
