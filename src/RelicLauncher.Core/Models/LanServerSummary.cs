namespace RelicLauncher.Core.Models;

public sealed record LanServerSummary
{
    public required string Address { get; init; }
    public string? ServerName { get; init; }
    public int Players { get; init; }
    public int MaxPlayers { get; init; }
    public string? GameVersion { get; init; }
    public bool HasPassword { get; init; }
    public string? Description { get; init; }
    public bool IsLocalHosted { get; init; }
}
