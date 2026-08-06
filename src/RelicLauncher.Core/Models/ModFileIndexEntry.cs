namespace RelicLauncher.Core.Models;

public sealed class ModFileIndexEntry
{
    public int FileId { get; init; }
    public string? FileName { get; init; }
    public string? ModId { get; init; }
}
