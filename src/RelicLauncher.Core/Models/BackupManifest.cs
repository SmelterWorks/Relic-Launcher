namespace RelicLauncher.Core.Models;

public sealed class BackupManifest
{
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset CreatedAt { get; init; }
    public string RelicVersion { get; init; } = string.Empty;
    public bool IncludesMods { get; init; }
    public bool IncludesWorlds { get; init; }
    public IReadOnlyList<string> Versions { get; init; } = [];
}
