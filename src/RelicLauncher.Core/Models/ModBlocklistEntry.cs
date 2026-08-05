namespace RelicLauncher.Core.Models;

public sealed class ModBlocklistEntry
{
    public required string Id { get; init; }
    public string? Reason { get; init; }

    public string ModId
    {
        get
        {
            var at = Id.IndexOf('@');
            return at <= 0 ? Id : Id[..at];
        }
    }

    public string? Version
    {
        get
        {
            var at = Id.IndexOf('@');
            return at < 0 || at >= Id.Length - 1 ? null : Id[(at + 1)..];
        }
    }
}
