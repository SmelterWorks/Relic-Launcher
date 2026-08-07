using System.Globalization;
using System.Text.Json;

namespace RelicLauncher.Core.Server;

public static class VintageStoryServerConfigReader
{
    public const int DefaultPort = 42420;

    public static int? TryReadPort(string serverDataPath)
    {
        if (string.IsNullOrWhiteSpace(serverDataPath))
        {
            return null;
        }

        var configPath = Path.Combine(serverDataPath.Trim(), "serverconfig.json");
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(configPath);
            using var doc = JsonDocument.Parse(stream);
            return TryReadPort(doc.RootElement);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static int? TryReadPort(JsonElement root)
    {
        foreach (var name in new[] { "Port", "port" })
        {
            if (root.TryGetProperty(name, out var portEl))
            {
                return portEl.ValueKind switch
                {
                    JsonValueKind.Number when portEl.TryGetInt32(out var port) => port,
                    JsonValueKind.String when int.TryParse(portEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                    _ => null,
                };
            }
        }

        return null;
    }
}
