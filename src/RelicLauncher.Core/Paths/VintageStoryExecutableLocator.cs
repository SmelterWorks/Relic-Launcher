namespace RelicLauncher.Core.Paths;

public static class VintageStoryExecutableLocator
{
    private static readonly string[] ClientCandidates =
    [
        "Vintagestory",
        "Vintagestory.exe",
        "Vintagestory.dll",
    ];

    public static string? FindClientExecutable(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }

        foreach (var candidate in ClientCandidates)
        {
            var fullPath = Path.Combine(installPath, candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}
