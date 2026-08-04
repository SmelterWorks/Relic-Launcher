using RelicLauncher.Core.Versions;

namespace RelicLauncher.Core.Models;

public sealed class ModReleaseInfo
{
    public required int FileId { get; init; }
    public required string ModVersion { get; init; }
    public string? FileName { get; init; }
    public IReadOnlyList<string> CompatibleGameVersions { get; init; } = [];
    public string? DownloadUrl { get; init; }

    public string DisplayLabel
    {
        get
        {
            if (CompatibleGameVersions.Count == 0)
            {
                return string.IsNullOrWhiteSpace(ModVersion) ? $"file {FileId}" : ModVersion;
            }

            var ordered = CompatibleGameVersions
                .OrderBy(v => v, Comparer<string>.Create(GameVersionComparer.Compare))
                .ToList();
            if (ordered.Count == 1)
            {
                return $"{ModVersion} · {ordered[0]}";
            }

            return $"{ModVersion} · {ordered[0]} .. {ordered[^1]}";
        }
    }
}
