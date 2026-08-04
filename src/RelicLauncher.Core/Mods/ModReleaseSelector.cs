using RelicLauncher.Core.Models;
using RelicLauncher.Core.Versions;

namespace RelicLauncher.Core.Mods;

public static class ModReleaseSelector
{
    public static ModReleaseInfo? SelectBest(IReadOnlyList<ModReleaseInfo> releases, string gameVersion)
    {
        if (releases.Count == 0 || string.IsNullOrWhiteSpace(gameVersion))
        {
            return null;
        }

        var matches = releases
            .Where(r => r.CompatibleGameVersions.Any(tag =>
                string.Equals(tag, gameVersion, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        return matches
            .OrderByDescending(r => r.ModVersion, Comparer<string>.Create(GameVersionComparer.Compare))
            .ThenByDescending(r => r.FileId)
            .First();
    }
}
