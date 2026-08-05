namespace RelicLauncher.Core.Models;

public sealed class ModDependencyIssue
{
    public required string DependentModId { get; init; }

    public required string RequiredModId { get; init; }

    public string? RequiredMinimumVersion { get; init; }

    public string? InstalledVersion { get; init; }

    public required ModDependencyIssueKind Kind { get; init; }

    public IReadOnlyList<string> CyclePath { get; init; } = [];
}
