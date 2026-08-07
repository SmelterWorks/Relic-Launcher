namespace RelicLauncher.Core.Paths;

public static class VintageStoryServerExecutableLocator
{
    private static readonly string[] ServerCandidates =
    [
        "VintagestoryServer",
        "VintagestoryServer.exe",
        "VintagestoryServer.dll",
    ];

    public static string? FindServerExecutable(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }

        foreach (var candidate in ServerCandidates)
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
