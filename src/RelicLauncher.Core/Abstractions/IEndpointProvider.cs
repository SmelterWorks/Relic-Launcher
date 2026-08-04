using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Abstractions;

public interface IEndpointProvider
{
    string AccountBaseUrl { get; }
    string CdnBaseUrl { get; }
    string ModDbApiBaseUrl { get; }
    string ModDbDownloadBaseUrl { get; }
    string VersionCatalogUrl { get; }
    string LatestStableUrl { get; }
    string NewsBlogUrl { get; }

    string BuildModDownloadUrl(int fileId);

    void Apply(LauncherSettings settings);

    void Apply(EndpointSettings endpoints);
}
