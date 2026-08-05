namespace RelicLauncher.Core.Models;

public sealed class ModDependencyAudit
{
    public IReadOnlyList<ModDependencyIssue> Issues { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<ModDependencyIssue>> IssuesByDependentModId { get; init; }
        = new Dictionary<string, IReadOnlyList<ModDependencyIssue>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ModDependencyRequirement> MissingExternalRequirements { get; init; } = [];

    public bool HasBlockingIssues
        => Issues.Any(i => i.Kind is ModDependencyIssueKind.Missing
            or ModDependencyIssueKind.Disabled
            or ModDependencyIssueKind.Outdated
            or ModDependencyIssueKind.BuiltinVersionMismatch
            or ModDependencyIssueKind.Cycle);
}
