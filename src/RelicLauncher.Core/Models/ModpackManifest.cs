namespace RelicLauncher.Core.Models;

public sealed class ModpackManifest
{
    public int SchemaVersion { get; init; } = 1;
    public string Format { get; init; } = "relic-modpack";
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string GameVersion { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string RelicVersion { get; init; } = string.Empty;
    public ModpackDistribution Distribution { get; init; } = ModpackDistribution.Online;
    public IReadOnlyList<ModpackModEntry> Mods { get; init; } = [];
}
