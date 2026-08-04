using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.Endpoints;

public sealed class EndpointProvider : IEndpointProvider
{
    private readonly Lock _gate = new();
    private EndpointSettings _endpoints = EndpointSettings.CreateDefaults();

    public string AccountBaseUrl
    {
        get { lock (_gate) { return NormalizeBase(_endpoints.AccountBaseUrl, VintageStoryEndpoints.AccountBaseUrl); } }
    }

    public string CdnBaseUrl
    {
        get { lock (_gate) { return NormalizeBase(_endpoints.CdnBaseUrl, VintageStoryEndpoints.CdnBaseUrl); } }
    }

    public string ModDbApiBaseUrl
    {
        get { lock (_gate) { return NormalizeBase(_endpoints.ModDbApiBaseUrl, VintageStoryEndpoints.ModDbApiBaseUrl); } }
    }

    public string ModDbDownloadBaseUrl
    {
        get { lock (_gate) { return NormalizeAbsolute(_endpoints.ModDbDownloadBaseUrl, VintageStoryEndpoints.ModDbDownloadBaseUrl); } }
    }

    public string VersionCatalogUrl
    {
        get { lock (_gate) { return NormalizeAbsolute(_endpoints.VersionCatalogUrl, VintageStoryEndpoints.VersionCatalogUrl); } }
    }

    public string LatestStableUrl
    {
        get { lock (_gate) { return NormalizeAbsolute(_endpoints.LatestStableUrl, VintageStoryEndpoints.LatestStableUrl); } }
    }

    public string NewsBlogUrl
    {
        get { lock (_gate) { return NormalizeAbsolute(_endpoints.NewsBlogUrl, VintageStoryEndpoints.NewsBlogUrl); } }
    }

    public string BuildModDownloadUrl(int fileId)
        => $"{ModDbDownloadBaseUrl}?fileid={fileId}";

    public void Apply(LauncherSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Apply(settings.Endpoints ?? EndpointSettings.CreateDefaults());
    }

    public void Apply(EndpointSettings endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        lock (_gate)
        {
            _endpoints = endpoints.Clone();
        }
    }

    private static string NormalizeBase(string? value, string fallback)
    {
        var normalized = NormalizeAbsolute(value, fallback);
        return normalized.EndsWith("/", StringComparison.Ordinal) ? normalized : normalized + "/";
    }

    private static string NormalizeAbsolute(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out _) ? trimmed : fallback;
    }
}
