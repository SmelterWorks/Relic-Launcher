using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.Servers;

public sealed partial class MasterServerClient
{
    internal static MasterServerCatalog? ParseCatalog(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var root = JsonNode.Parse(json) as JsonObject;
        if (root is null || !string.Equals(root["status"]?.GetValue<string>(), "ok", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var data = root["data"] as JsonArray;
        if (data is null)
        {
            return null;
        }

        var servers = new List<PublicServerSummary>(data.Count);
        foreach (var node in data)
        {
            if (TryParseServer(node as JsonObject, out var summary))
            {
                servers.Add(summary);
            }
        }

        return new MasterServerCatalog
        {
            Servers = servers,
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    private static bool TryParseServer(JsonObject? item, out PublicServerSummary summary)
    {
        summary = null!;
        if (item is null)
        {
            return false;
        }

        var address = item["serverIP"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var name = item["serverName"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = address;
        }

        var playStyle = item["playstyle"] as JsonObject;
        var mods = item["mods"] as JsonArray;
        summary = new PublicServerSummary
        {
            ServerName = name,
            ServerAddress = address,
            Players = item["players"]?.GetValue<int>() ?? 0,
            MaxPlayers = ParseInt(item["maxPlayers"]),
            GameVersion = item["gameVersion"]?.GetValue<string>(),
            HasPassword = item["hasPassword"]?.GetValue<bool>() ?? false,
            Whitelisted = item["whitelisted"]?.GetValue<bool>() ?? false,
            PlayStyleId = playStyle?["id"]?.GetValue<string>(),
            ModCount = mods?.Count ?? 0,
            Description = item["gameDescription"]?.GetValue<string>(),
            IsOfficialTopS = address.Contains("tops.vintagestory.at", StringComparison.OrdinalIgnoreCase),
        };
        return true;
    }

    private static int ParseInt(JsonNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        if (node is JsonValue value && value.TryGetValue<int>(out var intValue))
        {
            return intValue;
        }

        var text = node.GetValue<string>();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }
}
