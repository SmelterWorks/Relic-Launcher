namespace RelicLauncher.Core.Models;

public sealed class ModpackLocalSummary
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string GameVersion { get; init; }
    public ModpackDistribution Distribution { get; init; }
    public int ModCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public required string DirectoryPath { get; init; }
}
