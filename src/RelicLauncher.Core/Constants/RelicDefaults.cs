namespace RelicLauncher.Core.Constants;

public static class RelicDefaults
{
    public const string ThemeId = "relic-default";
    public const int ModBrowsePageSize = 24;
    public const int VersionBrowsePageSize = 30;
    public const int DebugLogCapacity = 300;
    public const string DisabledModSuffix = ".disabled";
    public const double HomeBackgroundLogoOpacity = 0.2;
    public const long MaxGameDownloadBytes = 4L * 1024 * 1024 * 1024;
    public const long MaxModDownloadBytes = 512L * 1024 * 1024;
    public const long MaxRemoteImageBytes = 8L * 1024 * 1024;
    public const int RemoteImageMemoryCacheEntries = 64;
}
