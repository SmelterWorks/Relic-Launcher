namespace RelicLauncher.Core.Constants;

public static class VintageStoryEndpoints
{
    public const string AccountBaseUrl = "https://account.vintagestory.at/";
    public const string CdnBaseUrl = "https://cdn.vintagestory.at/";
    public const string ModDbBaseUrl = "https://mods.vintagestory.at/";
    public const string ModDbApiBaseUrl = "https://mods.vintagestory.at/api/";
    public const string ModDbDownloadBaseUrl = "https://mods.vintagestory.at/download";
    public const string VersionCatalogUrl = "https://api.vintagestory.at/stable-unstable.json";
    public const string LatestStableUrl = "https://api.vintagestory.at/lateststable.txt";
    public const string LatestUnstableUrl = "https://api.vintagestory.at/latestunstable.txt";
    public const string NewsBlogUrl = "https://www.vintagestory.at/blog.html/";
    public const string WikiBaseUrl = "https://wiki.vintagestory.at/";
    public const string GameLoginUrl = "https://auth3.vintagestory.at/v2/gamelogin";
    public const string ClientValidateUrl = "https://auth3.vintagestory.at/clientvalidate";
    public const string ModBlocklistUrl = "https://cdn.vintagestory.at/api/blockedmods.json";
    public const string MasterServerListUrl = "https://masterserver.vintagestory.at/api/v1/servers/list";

    public static string BuildModDownloadUrl(int fileId)
        => $"{ModDbDownloadBaseUrl}?fileid={fileId}";

    public static string BuildModDbPageUrl(string? urlAlias, int modId)
    {
        if (!string.IsNullOrWhiteSpace(urlAlias))
        {
            return $"{ModDbBaseUrl}{urlAlias.Trim().TrimStart('/')}";
        }

        return $"{ModDbBaseUrl}#/details/{modId}";
    }
}
