using System.Text.Json;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Mods;

public static class ModInfoJsonParser
{
    public static ParsedModInfo? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return Parse(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ParsedModInfo Parse(JsonElement root)
    {
        var modId = ReadString(root, "modid") ?? ReadString(root, "modId");
        var name = ReadString(root, "name");
        var version = ReadString(root, "version");
        var iconPath = ReadString(root, "iconPath") ?? ReadString(root, "iconpath");
        var dependencies = ParseDependencies(root);

        return new ParsedModInfo
        {
            ModId = modId,
            Name = name,
            Version = version,
            IconPath = iconPath,
            Dependencies = dependencies,
        };
    }

    public static IReadOnlyList<ModDependencyRequirement> ParseDependencies(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, "dependencies", out var deps)
            || deps.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var list = new List<ModDependencyRequirement>();
        foreach (var property in deps.EnumerateObject())
        {
            var modId = property.Name?.Trim();
            if (string.IsNullOrWhiteSpace(modId))
            {
                continue;
            }

            string? minimum = null;
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                minimum = property.Value.GetString();
            }
            else if (property.Value.ValueKind is JsonValueKind.Number)
            {
                minimum = property.Value.GetRawText();
            }
            else if (property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                minimum = null;
            }
            else
            {
                continue;
            }

            list.Add(new ModDependencyRequirement
            {
                ModId = modId,
                MinimumVersion = string.IsNullOrWhiteSpace(minimum) ? null : minimum.Trim(),
            });
        }

        return list;
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!TryGetPropertyIgnoreCase(root, name, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string name, out JsonElement value)
    {
        if (root.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
