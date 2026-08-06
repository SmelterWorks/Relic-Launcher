using System.Globalization;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Mods;

public static class ModOriginClassifier
{
    private const string ModDbFilePrefix = "mod_";
    private const string ZipExtension = ".zip";

    public static ModOriginInfo Classify(
        LocalModInfo mod,
        IReadOnlyList<ModFileIndexEntry> indexEntries)
    {
        if (mod.IsDirectory)
        {
            return Local(mod);
        }

        var fileName = StripDisabled(mod.FileName);
        var fromName = TryParseFileIdFromName(fileName);
        if (fromName > 0)
        {
            return ModDb(fromName);
        }

        foreach (var entry in indexEntries)
        {
            if (entry.FileId <= 0)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.FileName)
                && string.Equals(entry.FileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return ModDb(entry.FileId);
            }

            if (!string.IsNullOrWhiteSpace(entry.ModId)
                && !string.IsNullOrWhiteSpace(mod.ModId)
                && string.Equals(entry.ModId, mod.ModId, StringComparison.OrdinalIgnoreCase))
            {
                return ModDb(entry.FileId);
            }
        }

        return Local(mod);
    }

    public static bool RequiresOfflinePack(IReadOnlyList<ModOriginInfo> origins)
        => origins.Any(o => o.Source == ModpackModSource.Local);

    private static ModOriginInfo ModDb(int fileId)
        => new() { Source = ModpackModSource.ModDb, FileId = fileId };

    private static ModOriginInfo Local(LocalModInfo mod)
        => new() { Source = ModpackModSource.Local, FileId = 0 };

    private static int TryParseFileIdFromName(string fileName)
    {
        if (!fileName.StartsWith(ModDbFilePrefix, StringComparison.OrdinalIgnoreCase)
            || !fileName.EndsWith(ZipExtension, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var middle = fileName.Substring(
            ModDbFilePrefix.Length,
            fileName.Length - ModDbFilePrefix.Length - ZipExtension.Length);
        return int.TryParse(middle, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileId)
            ? fileId
            : 0;
    }

    private static string StripDisabled(string name)
        => name.EndsWith(RelicDefaults.DisabledModSuffix, StringComparison.OrdinalIgnoreCase)
            ? name[..^RelicDefaults.DisabledModSuffix.Length]
            : name;
}
