namespace RelicLauncher.Core.Models;

public sealed class NewsContentBlock
{
    public NewsContentBlockKind Kind { get; init; }
    public string? Text { get; init; }
    public string? Url { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? Alt { get; init; }
}
