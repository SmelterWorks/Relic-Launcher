using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Sandbox;

public static class SandboxNetGrants
{
    public static IList<NetPortGrant> ForLauncherEndpoints(EndpointSettings endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var ports = new SortedSet<ushort>();
        AddUrlPort(endpoints.AccountBaseUrl, ports);
        AddUrlPort(endpoints.CdnBaseUrl, ports);
        AddUrlPort(endpoints.ModDbApiBaseUrl, ports);
        AddUrlPort(endpoints.ModDbDownloadBaseUrl, ports);
        AddUrlPort(endpoints.VersionCatalogUrl, ports);
        AddUrlPort(endpoints.LatestStableUrl, ports);
        AddUrlPort(endpoints.NewsBlogUrl, ports);
        AddUrlPort(endpoints.WikiBaseUrl, ports);
        AddUrlPort(endpoints.ServerListUrl, ports);

        AddUrlPort(VintageStoryEndpoints.GameLoginUrl, ports);
        AddUrlPort(VintageStoryEndpoints.ClientValidateUrl, ports);
        AddUrlPort(VintageStoryEndpoints.LatestUnstableUrl, ports);
        AddUrlPort(VintageStoryEndpoints.ModBlocklistUrl, ports);
        AddUrlPort(VintageStoryEndpoints.MasterServerListUrl, ports);
        AddUrlPort(RelicLauncherEndpoints.UpdatesBaseUrl, ports);
        AddUrlPort(RelicLauncherEndpoints.DownloadPageUrl, ports);

        EnsureCommonServicePorts(ports);
        return BuildOutboundConnectGrants(ports);
    }

    public static IList<NetPortGrant> ForGameClient()
    {
        var ports = new SortedSet<ushort>();
        EnsureCommonServicePorts(ports);
        return BuildOutboundConnectGrants(ports);
    }

    internal static void AddUrlPort(string? url, ISet<ushort> ports)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        ports.Add(ResolvePort(uri));
    }

    internal static ushort ResolvePort(Uri uri)
    {
        if (uri.Port > 0)
        {
            return (ushort)uri.Port;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? (ushort)443
            : (ushort)80;
    }

    private static void EnsureCommonServicePorts(ISet<ushort> ports)
    {
        ports.Add(53);
        ports.Add(80);
        ports.Add(443);
    }

    private static List<NetPortGrant> BuildOutboundConnectGrants(IEnumerable<ushort> ports)
    {
        var grants = new List<NetPortGrant>();
        foreach (var port in ports)
        {
            grants.Add(new NetPortGrant
            {
                Port = port,
                AllowConnectTcp = true,
                AllowConnectSendUdp = port is 53 or 443 or 80,
            });
        }

        return grants;
    }
}
