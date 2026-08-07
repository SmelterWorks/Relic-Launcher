namespace RelicLauncher.Core.Models;

public sealed class FavoriteServerEntry
{
    public required string Name { get; init; }
    public required string Address { get; init; }
    public DateTimeOffset SavedAt { get; init; }
}
