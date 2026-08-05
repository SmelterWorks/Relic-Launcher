namespace RelicLauncher.Core.Models;

public sealed class ModDependencyRequirement
{
    public required string ModId { get; init; }

    public string? MinimumVersion { get; init; }

    public bool AllowsAnyVersion
        => string.IsNullOrWhiteSpace(MinimumVersion)
           || string.Equals(MinimumVersion.Trim(), "*", StringComparison.Ordinal);
}
