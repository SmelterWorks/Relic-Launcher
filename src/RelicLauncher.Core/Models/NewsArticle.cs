namespace RelicLauncher.Core.Models;

public sealed class NewsArticle
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? Summary { get; init; }
    public string? PublishedLabel { get; init; }
}
