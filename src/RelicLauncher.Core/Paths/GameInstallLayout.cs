namespace RelicLauncher.Core.Paths;

public static class GameInstallLayout
{
    public const string VersionsFolderName = "versions";
    public const string InventoryFileName = "versions.json";

    public static string GetVersionsRoot(string installsRoot)
        => Path.Combine(installsRoot, VersionsFolderName);

    public static string GetVersionDirectory(string installsRoot, string version)
        => Path.Combine(GetVersionsRoot(installsRoot), version.Trim());

    public static string GetInventoryPath(string installsRoot)
        => Path.Combine(installsRoot, InventoryFileName);

    public static string GetModsDirectory(string dataPath)
        => Path.Combine(dataPath, "Mods");
}
