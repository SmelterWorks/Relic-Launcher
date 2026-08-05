namespace RelicLauncher.Core.Models;

public sealed class LocalModInfo
{
    public required string Path { get; init; }
    public required string FileName { get; init; }
    public string? ModId { get; init; }
    public string? Name { get; init; }
    public string? Version { get; init; }
    public string? IconPath { get; init; }
    public IReadOnlyList<ModDependencyRequirement> Dependencies { get; init; } = [];
    public bool IsEnabled { get; init; } = true;
    public bool IsDirectory { get; init; }
}
