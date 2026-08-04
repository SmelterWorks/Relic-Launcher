namespace RelicLauncher.Core.Models;

public sealed class ModSearchQuery
{
    public string? Text { get; init; }
    public string? OrderBy { get; init; } = "downloads";
    public string? OrderDirection { get; init; } = "desc";
    public string? GameVersion { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 24;
    public bool PreferCache { get; init; } = true;
}
