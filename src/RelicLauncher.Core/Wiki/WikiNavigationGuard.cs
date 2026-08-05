namespace RelicLauncher.Core.Wiki;

public static class WikiNavigationGuard
{
    public static WikiNavigationDecision Evaluate(string? wikiBaseUrl, string? candidateUrl, out Uri? resolvedAbsolute)
    {
        resolvedAbsolute = null;

        if (!TryParseAbsoluteBase(wikiBaseUrl, out var wikiBase))
        {
            return WikiNavigationDecision.Block;
        }

        if (string.IsNullOrWhiteSpace(candidateUrl))
        {
            return WikiNavigationDecision.Block;
        }

        var trimmed = candidateUrl.Trim();
        if (!Uri.TryCreate(wikiBase, trimmed, out var absolute) || !absolute.IsAbsoluteUri)
        {
            return WikiNavigationDecision.Block;
        }

        if (!IsAllowedScheme(absolute, wikiBase))
        {
            return WikiNavigationDecision.Block;
        }

        if (!string.IsNullOrEmpty(absolute.UserInfo))
        {
            return WikiNavigationDecision.Block;
        }

        resolvedAbsolute = absolute;

        if (!HostsMatch(wikiBase, absolute))
        {
            return WikiNavigationDecision.OpenExternally;
        }

        if (!PortsCompatible(wikiBase, absolute))
        {
            return WikiNavigationDecision.OpenExternally;
        }

        return WikiNavigationDecision.Allow;
    }

    public static bool TryParseAbsoluteBase(string? wikiBaseUrl, out Uri wikiBase)
    {
        wikiBase = null!;
        if (string.IsNullOrWhiteSpace(wikiBaseUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(wikiBaseUrl.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            return false;
        }

        wikiBase = parsed;
        return true;
    }

    private static bool IsAllowedScheme(Uri candidate, Uri wikiBase)
    {
        if (string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(candidate.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && string.Equals(wikiBase.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HostsMatch(Uri wikiBase, Uri candidate)
        => string.Equals(wikiBase.IdnHost, candidate.IdnHost, StringComparison.OrdinalIgnoreCase);

    private static bool PortsCompatible(Uri wikiBase, Uri candidate)
        => wikiBase.Port == candidate.Port;
}
