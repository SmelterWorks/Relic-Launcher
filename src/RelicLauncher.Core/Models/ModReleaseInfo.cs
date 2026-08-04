namespace RelicLauncher.Core.Models;

public sealed class ModReleaseInfo
{
    public required int FileId { get; init; }
    public required string ModVersion { get; init; }
    public string? FileName { get; init; }
    public IReadOnlyList<string> CompatibleGameVersions { get; init; } = [];
    public string? DownloadUrl { get; init; }
}
