namespace RelicLauncher.Core.Models;

public sealed class ModSummary
{
    public required int ModId { get; init; }
    public int AssetId { get; init; }
    public required string Name { get; init; }
    public string? Author { get; init; }
    public string? Summary { get; init; }
    public int Downloads { get; init; }
    public int Follows { get; init; }
    public int TrendingPoints { get; init; }
    public string? UrlAlias { get; init; }
    public string? Side { get; init; }
    public string? LogoUrl { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? LastReleased { get; init; }
}
