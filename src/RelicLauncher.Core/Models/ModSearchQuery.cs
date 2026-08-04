using RelicLauncher.Core.Constants;

namespace RelicLauncher.Core.Models;

public sealed class ModSearchQuery
{
    public string? Text { get; init; }
    public string? OrderBy { get; init; } = "downloads";
    public string? OrderDirection { get; init; } = "desc";
    public string? GameVersion { get; init; }
    public string? Side { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = RelicDefaults.ModBrowsePageSize;
    public bool PreferCache { get; init; } = true;
}
