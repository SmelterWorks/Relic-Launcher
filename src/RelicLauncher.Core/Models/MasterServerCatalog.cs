namespace RelicLauncher.Core.Models;

public sealed class MasterServerCatalog
{
    public required IReadOnlyList<PublicServerSummary> Servers { get; init; }
    public DateTimeOffset FetchedAt { get; init; }
}
