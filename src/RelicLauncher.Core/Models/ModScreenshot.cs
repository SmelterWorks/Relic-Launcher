namespace RelicLauncher.Core.Models;

public sealed class ModScreenshot
{
    public int FileId { get; init; }
    public string? MainUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? FileName { get; init; }
}
