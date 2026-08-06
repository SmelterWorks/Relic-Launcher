namespace RelicLauncher.Core.Models;

public sealed class BackupRequest
{
    public required string DestinationZipPath { get; init; }
    public string? DataPath { get; init; }
    public string? InstallsRoot { get; init; }
    public bool IncludeMods { get; init; }
    public bool IncludeWorlds { get; init; }
    public IReadOnlyList<string> VersionsToInclude { get; init; } = [];
    public IProgress<double>? Progress { get; init; }
}
