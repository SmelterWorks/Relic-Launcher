namespace RelicLauncher.Core.Models;

public sealed class ModpackSaveRequest
{
    public required string DataPath { get; init; }
    public required string GameVersion { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<LocalModInfo> Mods { get; init; } = [];
}
