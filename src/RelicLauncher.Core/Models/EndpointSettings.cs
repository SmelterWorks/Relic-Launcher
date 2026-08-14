using RelicLauncher.Core.Constants;

namespace RelicLauncher.Core.Models;

public sealed class EndpointSettings
{
    public string AccountBaseUrl { get; set; } = VintageStoryEndpoints.AccountBaseUrl;
    public string CdnBaseUrl { get; set; } = VintageStoryEndpoints.CdnBaseUrl;
    public string ModDbApiBaseUrl { get; set; } = VintageStoryEndpoints.ModDbApiBaseUrl;
    public string ModDbDownloadBaseUrl { get; set; } = VintageStoryEndpoints.ModDbDownloadBaseUrl;
    public string VersionCatalogUrl { get; set; } = VintageStoryEndpoints.VersionCatalogUrl;
    public string LatestStableUrl { get; set; } = VintageStoryEndpoints.LatestStableUrl;
    public string NewsBlogUrl { get; set; } = VintageStoryEndpoints.NewsBlogUrl;
    public string WikiBaseUrl { get; set; } = VintageStoryEndpoints.WikiBaseUrl;
    public string ServerListUrl { get; set; } = RelicLauncherEndpoints.ServerListUrl;
    public string PanelApiBaseUrl { get; set; } = RelicLauncherEndpoints.PanelApiBaseUrl;

    public static EndpointSettings CreateDefaults() => new();

    public EndpointSettings Clone()
        => new()
        {
            AccountBaseUrl = AccountBaseUrl,
            CdnBaseUrl = CdnBaseUrl,
            ModDbApiBaseUrl = ModDbApiBaseUrl,
            ModDbDownloadBaseUrl = ModDbDownloadBaseUrl,
            VersionCatalogUrl = VersionCatalogUrl,
            LatestStableUrl = LatestStableUrl,
            NewsBlogUrl = NewsBlogUrl,
            WikiBaseUrl = WikiBaseUrl,
            ServerListUrl = ServerListUrl,
            PanelApiBaseUrl = PanelApiBaseUrl,
        };
}
