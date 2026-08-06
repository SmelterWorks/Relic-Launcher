namespace RelicLauncher.Core.Models;

public sealed class ModpackModEntry
{
    public required string ModId { get; init; }
    public string? ModVersion { get; init; }
    public int FileId { get; init; }
    public bool Enabled { get; init; } = true;
    public ModpackModSource Source { get; init; } = ModpackModSource.ModDb;
    public string? ArchivePath { get; init; }
}
