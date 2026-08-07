namespace RelicLauncher.Core.Paths;

using RelicLauncher.Core.Models;

public static class GameServerInstallLayout
{
    public const string ServersFolderName = "servers";
    public const string InventoryFileName = "servers.json";

    public static string GetServersRoot(string installsRoot)
        => Path.Combine(installsRoot, ServersFolderName);

    public static string GetServerDirectory(string installsRoot, string version)
        => Path.Combine(GetServersRoot(installsRoot), version.Trim());

    public static string GetInventoryPath(string installsRoot)
        => Path.Combine(installsRoot, InventoryFileName);

    public static string ResolveDefaultServerDataPath(HostOs os)
    {
        return os switch
        {
            HostOs.Windows => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VintagestoryServerData"),
            HostOs.MacOs => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                "VintagestoryServerData"),
            _ => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VintagestoryServerData"),
        };
    }
}
