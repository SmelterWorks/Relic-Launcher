namespace RelicLauncher.Core.Models;

public sealed class ModpackExportRequest
{
    public required string DestinationPath { get; init; }
    public required string DataPath { get; init; }
    public required string GameVersion { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<LocalModInfo> Mods { get; init; } = [];
    public IProgress<double>? Progress { get; init; }
}
