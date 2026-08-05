using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Mods;

public sealed class ParsedModInfo
{
    public string? ModId { get; init; }

    public string? Name { get; init; }

    public string? Version { get; init; }

    public string? IconPath { get; init; }

    public IReadOnlyList<ModDependencyRequirement> Dependencies { get; init; } = [];
}
