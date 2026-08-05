namespace RelicLauncher.Infrastructure.DotNet;

internal static class DotNetRuntimeLayout
{
    public static string GetManagedRoot(string cacheDirectory, int majorVersion)
        => Path.Combine(cacheDirectory, "dotnet", $"net{majorVersion}");

    public static string GetDownloadDirectory(string cacheDirectory)
        => Path.Combine(cacheDirectory, "downloads");

    public static string GetNetCoreSharedDir(string dotNetRoot)
        => Path.Combine(dotNetRoot, "shared", "Microsoft.NETCore.App");

    public static string GetWindowsDesktopSharedDir(string dotNetRoot)
        => Path.Combine(dotNetRoot, "shared", "Microsoft.WindowsDesktop.App");

    public static bool HasRequiredSharedFrameworks(string dotNetRoot, int majorVersion, bool requireWindowsDesktop)
    {
        if (!HasMajorSharedFramework(GetNetCoreSharedDir(dotNetRoot), majorVersion))
        {
            return false;
        }

        if (requireWindowsDesktop
            && !HasMajorSharedFramework(GetWindowsDesktopSharedDir(dotNetRoot), majorVersion))
        {
            return false;
        }

        return true;
    }

    public static bool HasMajorSharedFramework(string sharedFrameworkDir, int majorVersion)
    {
        if (!Directory.Exists(sharedFrameworkDir))
        {
            return false;
        }

        var prefix = majorVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";
        return Directory.EnumerateDirectories(sharedFrameworkDir)
            .Select(Path.GetFileName)
            .Any(name => name is not null
                         && name.StartsWith(prefix, StringComparison.Ordinal)
                         && char.IsDigit(name[prefix.Length]));
    }
}
