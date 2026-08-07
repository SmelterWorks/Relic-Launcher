namespace RelicLauncher.Core.Models;

public sealed record PublicServerSummary
{
    public required string ServerName { get; init; }
    public required string ServerAddress { get; init; }
    public int Players { get; init; }
    public int MaxPlayers { get; init; }
    public string? GameVersion { get; init; }
    public bool HasPassword { get; init; }
    public bool Whitelisted { get; init; }
    public string? PlayStyleId { get; init; }
    public int ModCount { get; init; }
    public string? Description { get; init; }
    public bool IsOfficialTopS { get; init; }
}
