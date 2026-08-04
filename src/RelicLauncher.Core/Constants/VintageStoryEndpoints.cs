namespace RelicLauncher.Core.Constants;

public static class VintageStoryEndpoints
{
    public const string AccountBaseUrl = "https://account.vintagestory.at/";
    public const string CdnBaseUrl = "https://cdn.vintagestory.at/";
    public const string ModDbApiBaseUrl = "https://mods.vintagestory.at/api/";
    public const string ModDbDownloadBaseUrl = "https://mods.vintagestory.at/download";
    public const string VersionCatalogUrl = "https://api.vintagestory.at/stable-unstable.json";
    public const string LatestStableUrl = "https://api.vintagestory.at/lateststable.txt";
    public const string LatestUnstableUrl = "https://api.vintagestory.at/latestunstable.txt";
    public const string NewsBlogUrl = "https://www.vintagestory.at/blog.html/";
    public const string GameLoginUrl = "https://auth3.vintagestory.at/v2/gamelogin";
    public const string ClientValidateUrl = "https://auth3.vintagestory.at/clientvalidate";

    public static string BuildModDownloadUrl(int fileId)
        => $"{ModDbDownloadBaseUrl}?fileid={fileId}";
}
