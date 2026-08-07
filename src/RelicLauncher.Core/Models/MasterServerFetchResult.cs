namespace RelicLauncher.Core.Models;

public sealed class MasterServerFetchResult
{
    public required MasterServerCatalog Catalog { get; init; }
    public bool FromCache { get; init; }
    public bool IsStale { get; init; }
    public bool UsedOfficialFallback { get; init; }
}
