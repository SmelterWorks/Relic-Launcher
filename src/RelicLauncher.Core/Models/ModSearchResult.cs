namespace RelicLauncher.Core.Models;

public sealed class ModSearchResult
{
    public required IReadOnlyList<ModSummary> Mods { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool FromCache { get; init; }
    public bool IsStale { get; init; }
}
