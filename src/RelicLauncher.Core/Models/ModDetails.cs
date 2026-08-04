namespace RelicLauncher.Core.Models;

public sealed class ModDetails
{
    public required int ModId { get; init; }
    public int AssetId { get; init; }
    public string? UrlAlias { get; init; }
    public required string Name { get; init; }
    public string? Author { get; init; }
    public string? Summary { get; init; }
    public string? DescriptionText { get; init; }
    public string? Side { get; init; }
    public string? LogoUrl { get; init; }
    public int Downloads { get; init; }
    public int Follows { get; init; }
    public string? HomepageUrl { get; init; }
    public string? WikiUrl { get; init; }
    public string? SourceCodeUrl { get; init; }
    public string? TrailerVideoUrl { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<ModScreenshot> Screenshots { get; init; } = [];
    public IReadOnlyList<ModReleaseInfo> Releases { get; init; } = [];
}
