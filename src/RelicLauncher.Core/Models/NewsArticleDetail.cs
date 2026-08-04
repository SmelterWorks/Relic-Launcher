namespace RelicLauncher.Core.Models;

public sealed class NewsArticleDetail
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? PublishedLabel { get; init; }
    public required string Body { get; init; }
}
