using System.Text.Json;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Updates;

public static class UpdateManifestParser
{
    private const int SupportedSchemaVersion = 1;

    public static Result<LauncherUpdateInfo> Parse(string json, LauncherUpdateChannel channel)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var validation = ValidateRoot(root);
            if (!validation.IsSuccess)
            {
                return Result<LauncherUpdateInfo>.Failure(validation.Error!);
            }

            var version = root.GetProperty("version").GetString()!;
            var releaseNotesUrl = root.TryGetProperty("releaseNotesUrl", out var notesProp) &&
                                  notesProp.ValueKind == JsonValueKind.String &&
                                  !string.IsNullOrWhiteSpace(notesProp.GetString())
                ? notesProp.GetString()!
                : RelicLauncherEndpoints.DownloadPageUrl;

            var assets = ParseAssets(root.GetProperty("assets"));
            if (assets.Count == 0)
            {
                return Result<LauncherUpdateInfo>.Failure("Update manifest has no usable assets.");
            }

            return Result<LauncherUpdateInfo>.Success(new LauncherUpdateInfo
            {
                Version = version,
                ReleaseNotesUrl = releaseNotesUrl,
                Channel = channel,
                Assets = assets,
            });
        }
        catch (JsonException ex)
        {
            return Result<LauncherUpdateInfo>.Failure($"Invalid update manifest JSON: {ex.Message}");
        }
    }

    internal static bool IsAllowedAssetUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(uri.Host, RelicLauncherEndpoints.UpdatesFileHost, StringComparison.OrdinalIgnoreCase);
    }

    private static Result ValidateRoot(JsonElement root)
    {
        if (!root.TryGetProperty("schemaVersion", out var schemaProp) ||
            schemaProp.ValueKind != JsonValueKind.Number ||
            schemaProp.GetInt32() != SupportedSchemaVersion)
        {
            return Result.Failure("Unsupported update manifest schema.");
        }

        if (!TryReadString(root, "product", out var product) ||
            !string.Equals(product, "relic", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("Unexpected update product.");
        }

        if (!TryReadString(root, "version", out _))
        {
            return Result.Failure("Update manifest is missing version.");
        }

        if (!root.TryGetProperty("assets", out var assetsElement) ||
            assetsElement.ValueKind != JsonValueKind.Array)
        {
            return Result.Failure("Update manifest is missing assets.");
        }

        return Result.Success();
    }

    private static List<LauncherUpdateAsset> ParseAssets(JsonElement assetsElement)
    {
        var assets = new List<LauncherUpdateAsset>();
        foreach (var assetElement in assetsElement.EnumerateArray())
        {
            var asset = ParseAsset(assetElement);
            if (asset is not null)
            {
                assets.Add(asset);
            }
        }

        return assets;
    }

    private static LauncherUpdateAsset? ParseAsset(JsonElement assetElement)
    {
        if (!TryReadString(assetElement, "installKind", out var installKind) ||
            !TryReadString(assetElement, "rid", out var rid) ||
            !TryReadString(assetElement, "filename", out var filename) ||
            !TryReadString(assetElement, "url", out var url) ||
            !TryReadString(assetElement, "sha256", out var sha256) ||
            !IsAllowedAssetUrl(url))
        {
            return null;
        }

        long sizeBytes = 0;
        if (assetElement.TryGetProperty("sizeBytes", out var sizeProp) &&
            sizeProp.ValueKind == JsonValueKind.Number)
        {
            sizeProp.TryGetInt64(out sizeBytes);
        }

        return new LauncherUpdateAsset
        {
            InstallKind = installKind,
            Rid = rid,
            Filename = filename,
            Url = url,
            Sha256 = sha256,
            SizeBytes = sizeBytes,
        };
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = prop.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
