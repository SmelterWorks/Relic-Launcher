using NuGet.Versioning;

namespace RelicLauncher.Infrastructure.Updates;

public static class LauncherVersionComparer
{
    public static bool IsUpdateAvailable(string currentVersion, string remoteVersion)
    {
        if (!NuGetVersion.TryParse(Normalize(currentVersion), out var current) ||
            !NuGetVersion.TryParse(Normalize(remoteVersion), out var remote))
        {
            return false;
        }

        return remote > current;
    }

    private static string Normalize(string version)
    {
        var trimmed = version.Trim();
        return trimmed.StartsWith('v') || trimmed.StartsWith('V')
            ? trimmed[1..]
            : trimmed;
    }
}
