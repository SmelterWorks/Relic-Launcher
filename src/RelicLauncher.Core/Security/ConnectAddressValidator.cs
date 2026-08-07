using System.Globalization;
using System.Net;
using RelicLauncher.Core.Server;

namespace RelicLauncher.Core.Security;

public static class ConnectAddressValidator
{
    public const string InvalidAddressMessage = "Enter a valid host, IP address, or host:port.";

    public static bool TryNormalize(string? input, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = InvalidAddressMessage;
            return false;
        }

        var trimmed = StripJoinScheme(input.Trim());
        if (!TryRejectUnsafe(trimmed, out error))
        {
            return false;
        }

        if (trimmed.StartsWith('['))
        {
            return TryNormalizeBracketed(trimmed, out normalized, out error);
        }

        return TryNormalizeHostPort(trimmed, out normalized, out error);
    }

    private static bool TryRejectUnsafe(string trimmed, out string? error)
    {
        error = null;
        if (trimmed.Contains(' ') || trimmed.Contains('@') || trimmed.Contains('/') || trimmed.Contains('\\'))
        {
            error = InvalidAddressMessage;
            return false;
        }

        if (trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            error = InvalidAddressMessage;
            return false;
        }

        return true;
    }

    private static bool TryNormalizeBracketed(string trimmed, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;
        var close = trimmed.IndexOf(']');
        if (close < 0)
        {
            error = InvalidAddressMessage;
            return false;
        }

        var ipv6 = trimmed[1..close];
        if (!IPAddress.TryParse(ipv6, out var parsed) ||
            parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            error = InvalidAddressMessage;
            return false;
        }

        var port = VintageStoryServerConfigReader.DefaultPort;
        if (close + 1 < trimmed.Length)
        {
            if (trimmed[close + 1] != ':')
            {
                error = InvalidAddressMessage;
                return false;
            }

            if (!TryParsePort(trimmed[(close + 2)..], out port, out error))
            {
                return false;
            }
        }

        normalized = $"[{parsed}]:{port}";
        return true;
    }

    private static bool TryNormalizeHostPort(string trimmed, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;
        var hostPart = trimmed;
        var port = VintageStoryServerConfigReader.DefaultPort;
        var lastColon = trimmed.LastIndexOf(':');
        if (lastColon > 0 && trimmed.Count(c => c == ':') == 1)
        {
            if (!TryParsePort(trimmed[(lastColon + 1)..], out port, out error))
            {
                return false;
            }

            hostPart = trimmed[..lastColon];
        }

        if (string.IsNullOrWhiteSpace(hostPart))
        {
            error = InvalidAddressMessage;
            return false;
        }

        if (IPAddress.TryParse(hostPart, out var ip))
        {
            normalized = $"{ip}:{port}";
            return true;
        }

        if (!IsValidHostname(hostPart))
        {
            error = InvalidAddressMessage;
            return false;
        }

        normalized = $"{hostPart.ToLowerInvariant()}:{port}";
        return true;
    }

    private static string StripJoinScheme(string value)
    {
        const string prefix = "vintagestoryjoin://";
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..]
            : value;
    }

    private static bool TryParsePort(string segment, out int port, out string? error)
    {
        error = null;
        if (!int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out port))
        {
            error = InvalidAddressMessage;
            return false;
        }

        if (port is < 1 or > 65535)
        {
            error = InvalidAddressMessage;
            return false;
        }

        return true;
    }

    private static bool IsValidHostname(string host)
    {
        if (host.Length is 0 or > 253 || host.StartsWith('.') || host.EndsWith('.'))
        {
            return false;
        }

        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (labels.Length == 0)
        {
            return false;
        }

        foreach (var label in labels)
        {
            if (label.Length is 0 or > 63)
            {
                return false;
            }

            if (!label.All(static ch => char.IsAsciiLetterOrDigit(ch) || ch is '-'))
            {
                return false;
            }

            if (label.StartsWith('-') || label.EndsWith('-'))
            {
                return false;
            }
        }

        return true;
    }
}
