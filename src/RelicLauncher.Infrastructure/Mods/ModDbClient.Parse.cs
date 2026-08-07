using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Endpoints;

namespace RelicLauncher.Infrastructure.Mods;

public sealed partial class ModDbClient
{
    public static IReadOnlyList<ModTagInfo> ParseTags(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("tags", out var tagsEl) || tagsEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<ModTagInfo>();
        foreach (var tag in tagsEl.EnumerateArray())
        {
            var id = tag.TryGetProperty("tagid", out var idEl)
                ? idEl.ValueKind == JsonValueKind.Number
                    ? idEl.GetInt32().ToString(CultureInfo.InvariantCulture)
                    : idEl.GetString()
                : null;
            var name = tag.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            list.Add(new ModTagInfo
            {
                TagId = id,
                Name = name,
                Color = tag.TryGetProperty("color", out var color) ? color.GetString() : null,
            });
        }

        return list
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ModSummary> ParseSearch(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("mods", out var modsEl))
        {
            return [];
        }

        if (modsEl.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Mod catalog mods field must be a JSON array.");
        }

        var list = new List<ModSummary>();
        foreach (var mod in modsEl.EnumerateArray())
        {
            list.Add(ParseSummary(mod));
        }

        return list;
    }

    public static ModDetails? ParseDetails(string json, Func<int, string>? buildDownloadUrl = null)
    {
        buildDownloadUrl ??= VintageStoryEndpoints.BuildModDownloadUrl;
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("mod", out var mod))
        {
            return null;
        }

        var html = mod.TryGetProperty("text", out var textEl) ? textEl.GetString() : null;
        var logo = FirstNonEmpty(
            mod.TryGetProperty("logofilename", out var lf) ? lf.GetString() : null,
            mod.TryGetProperty("logofile", out var lo) ? lo.GetString() : null,
            mod.TryGetProperty("logofiledb", out var ldb) ? ldb.GetString() : null);

        return new ModDetails
        {
            ModId = mod.TryGetProperty("modid", out var id) ? id.GetInt32() : 0,
            AssetId = mod.TryGetProperty("assetid", out var asset) ? asset.GetInt32() : 0,
            UrlAlias = mod.TryGetProperty("urlalias", out var alias) ? alias.GetString() : null,
            Name = mod.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "Unknown" : "Unknown",
            Author = mod.TryGetProperty("author", out var author) ? author.GetString() : null,
            Summary = mod.TryGetProperty("summary", out var summary) ? summary.GetString() : null,
            DescriptionText = StripHtml(html),
            Side = mod.TryGetProperty("side", out var side) ? side.GetString() : null,
            LogoUrl = logo,
            Downloads = mod.TryGetProperty("downloads", out var downloads) ? downloads.GetInt32() : 0,
            Follows = mod.TryGetProperty("follows", out var follows) ? follows.GetInt32() : 0,
            HomepageUrl = mod.TryGetProperty("homepageurl", out var home) ? home.GetString() : null,
            WikiUrl = mod.TryGetProperty("wikiurl", out var wiki) ? wiki.GetString() : null,
            SourceCodeUrl = mod.TryGetProperty("sourcecodeurl", out var source) ? source.GetString() : null,
            TrailerVideoUrl = mod.TryGetProperty("trailervideourl", out var trailer) ? trailer.GetString() : null,
            Tags = ReadStringArray(mod, "tags"),
            Screenshots = ParseScreenshots(mod),
            Releases = ParseReleases(mod, buildDownloadUrl),
        };
    }

    private static IReadOnlyList<ModReleaseInfo> ParseReleases(JsonElement mod, Func<int, string> buildDownloadUrl)
    {
        var releases = new List<ModReleaseInfo>();
        if (!mod.TryGetProperty("releases", out var releasesEl) || releasesEl.ValueKind != JsonValueKind.Array)
        {
            return releases;
        }

        foreach (var release in releasesEl.EnumerateArray())
        {
            var fileId = release.TryGetProperty("fileid", out var fid) ? fid.GetInt32() : 0;
            if (fileId <= 0)
            {
                continue;
            }

            releases.Add(new ModReleaseInfo
            {
                FileId = fileId,
                ModVersion = release.TryGetProperty("modversion", out var ver) ? ver.GetString() ?? string.Empty : string.Empty,
                FileName = release.TryGetProperty("filename", out var fn) ? fn.GetString() : null,
                CompatibleGameVersions = ReadStringArray(release, "tags"),
                DownloadUrl = release.TryGetProperty("mainfile", out var main) && !string.IsNullOrWhiteSpace(main.GetString())
                    ? main.GetString()!
                    : buildDownloadUrl(fileId),
            });
        }

        return releases;
    }

    private static IReadOnlyList<ModScreenshot> ParseScreenshots(JsonElement mod)
    {
        var screenshots = new List<ModScreenshot>();
        if (!mod.TryGetProperty("screenshots", out var shotsEl) || shotsEl.ValueKind != JsonValueKind.Array)
        {
            return screenshots;
        }

        foreach (var shot in shotsEl.EnumerateArray())
        {
            screenshots.Add(new ModScreenshot
            {
                FileId = shot.TryGetProperty("fileid", out var sid) ? sid.GetInt32() : 0,
                MainUrl = shot.TryGetProperty("mainfile", out var main) ? main.GetString() : null,
                ThumbnailUrl = shot.TryGetProperty("thumbnailfilename", out var thumb) ? thumb.GetString() : null,
                FileName = shot.TryGetProperty("filename", out var name) ? name.GetString() : null,
            });
        }

        return screenshots;
    }

    private static ModSummary ParseSummary(JsonElement mod)
    {
        return new ModSummary
        {
            ModId = mod.TryGetProperty("modid", out var id) ? id.GetInt32() : 0,
            AssetId = mod.TryGetProperty("assetid", out var asset) ? asset.GetInt32() : 0,
            Name = mod.TryGetProperty("name", out var name) ? name.GetString() ?? "Unknown" : "Unknown",
            Author = mod.TryGetProperty("author", out var author) ? author.GetString() : null,
            Summary = mod.TryGetProperty("summary", out var summary) ? summary.GetString() : null,
            Downloads = mod.TryGetProperty("downloads", out var downloads) ? downloads.GetInt32() : 0,
            Follows = mod.TryGetProperty("follows", out var follows) ? follows.GetInt32() : 0,
            TrendingPoints = mod.TryGetProperty("trendingpoints", out var trending) ? trending.GetInt32() : 0,
            UrlAlias = mod.TryGetProperty("urlalias", out var alias) ? alias.GetString() : null,
            Side = mod.TryGetProperty("side", out var side) ? side.GetString() : null,
            LogoUrl = mod.TryGetProperty("logo", out var logo) ? logo.GetString() : null,
            Tags = ReadStringArray(mod, "tags"),
            LastReleased = mod.TryGetProperty("lastreleased", out var last) ? last.GetString() : null,
        };
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                list.Add(value);
            }
        }

        return list;
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var normalized = html
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</div>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</h1>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</h2>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</h3>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</tr>", "\n", StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder(normalized.Length);
        var inTag = false;
        foreach (var ch in normalized)
        {
            if (ch == '<')
            {
                inTag = true;
                continue;
            }

            if (ch == '>')
            {
                inTag = false;
                continue;
            }

            if (!inTag)
            {
                sb.Append(ch);
            }
        }

        var text = WebUtility.HtmlDecode(sb.ToString());
        return NormalizeDescriptionWhitespace(text);
    }

    private static string NormalizeDescriptionWhitespace(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var cleaned = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            cleaned.Add(CollapseHorizontalWhitespace(line).TrimEnd());
        }

        var sb = new StringBuilder(text.Length);
        var blankRun = 0;
        foreach (var line in cleaned)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                blankRun++;
                if (blankRun <= 2)
                {
                    sb.Append('\n');
                }

                continue;
            }

            blankRun = 0;
            if (sb.Length > 0 && sb[^1] != '\n')
            {
                sb.Append('\n');
            }

            sb.Append(line);
            sb.Append('\n');
        }

        return sb.ToString().Trim();
    }

    private static string CollapseHorizontalWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var previousWasSpace = false;
        foreach (var ch in text)
        {
            if (ch is ' ' or '\t')
            {
                if (!previousWasSpace)
                {
                    sb.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            previousWasSpace = false;
            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

}
