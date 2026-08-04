using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Mods;

public sealed class ModReleaseResolver : IModReleaseResolver
{
    private readonly HttpClient _httpClient;
    private readonly IModDbClient _modDb;
    private readonly IEndpointProvider _endpoints;
    private readonly ILogger<ModReleaseResolver> _logger;

    public ModReleaseResolver(
        IModDbClient modDb,
        IEndpointProvider endpoints,
        ILogger<ModReleaseResolver> logger)
        : this(modDb, endpoints, logger, CreateDefaultClient())
    {
    }

    internal ModReleaseResolver(
        IModDbClient modDb,
        IEndpointProvider endpoints,
        ILogger<ModReleaseResolver> logger,
        HttpClient httpClient)
    {
        _modDb = modDb;
        _endpoints = endpoints;
        _logger = logger;
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RelicLauncher", "0.1.0"));
        }
    }

    public async Task<Result<ModReleaseInfo>> ResolveAsync(
        string modIdentifier,
        string gameVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modIdentifier))
        {
            return Result<ModReleaseInfo>.Failure("Mod identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            return Result<ModReleaseInfo>.Failure("Game version is required.");
        }

        var id = modIdentifier.Trim();
        var gv = gameVersion.Trim();
        var fromV2 = await TryResolveFromV2Async(id, gv, cancellationToken).ConfigureAwait(false);
        if (fromV2.IsSuccess)
        {
            return fromV2;
        }

        _logger.LogDebug(
            "ModDB v2 install-information unavailable for {Mod}@{Version}: {Error}. Falling back to v1 tags.",
            id,
            gv,
            fromV2.Error);

        return await ResolveFromV1Async(id, gv, cancellationToken).ConfigureAwait(false);
    }

    public static ModReleaseInfo? SelectBestRelease(IReadOnlyList<ModReleaseInfo> releases, string gameVersion)
        => ModReleaseSelector.SelectBest(releases, gameVersion);

    private async Task<Result<ModReleaseInfo>> TryResolveFromV2Async(
        string modIdentifier,
        string gameVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var url =
                $"{_endpoints.ModDbApiBaseUrl}v2/mods/install-information?ids={Uri.EscapeDataString(modIdentifier)}&gv={Uri.EscapeDataString(gameVersion)}";
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<ModReleaseInfo>.Failure(
                    $"ModDB v2 install-information returned {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                return Result<ModReleaseInfo>.Failure("ModDB v2 install-information response missing data.");
            }

            if (!TryGetInstallEntry(data, modIdentifier, out var entry))
            {
                return Result<ModReleaseInfo>.Failure("ModDB v2 install-information returned no entry.");
            }

            return ParseInstallEntry(entry, gameVersion);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            return Result<ModReleaseInfo>.Failure(ex.Message);
        }
    }

    private Result<ModReleaseInfo> ParseInstallEntry(JsonElement entry, string gameVersion)
    {
        if (entry.TryGetProperty("errorCode", out var errorCode) && errorCode.ValueKind == JsonValueKind.Number)
        {
            var reason = entry.TryGetProperty("retractionReason", out var rr) ? rr.GetString() : null;
            return Result<ModReleaseInfo>.Failure(
                string.IsNullOrWhiteSpace(reason)
                    ? $"ModDB v2 could not resolve release (error {errorCode.GetInt32()})."
                    : reason);
        }

        var fileName = entry.TryGetProperty("fileName", out var fn) ? fn.GetString() : null;
        var fileUrl = entry.TryGetProperty("fileUrl", out var fu) ? fu.GetString() : null;
        var modVersion = entry.TryGetProperty("recommendedUpgrade", out var ru) ? ru.GetString() : null;
        if (string.IsNullOrWhiteSpace(modVersion) && !string.IsNullOrWhiteSpace(fileName))
        {
            modVersion = Path.GetFileNameWithoutExtension(fileName);
        }

        var fileId = TryParseFileId(fileUrl);
        if (fileId is null or <= 0)
        {
            return Result<ModReleaseInfo>.Failure("ModDB v2 install-information missing file id.");
        }

        return Result<ModReleaseInfo>.Success(new ModReleaseInfo
        {
            FileId = fileId.Value,
            ModVersion = modVersion ?? string.Empty,
            FileName = fileName,
            CompatibleGameVersions = [gameVersion],
            DownloadUrl = ResolveDownloadUrl(fileUrl, fileId.Value),
        });
    }

    private static bool TryGetInstallEntry(JsonElement data, string modIdentifier, out JsonElement entry)
    {
        foreach (var property in data.EnumerateObject())
        {
            if (string.Equals(property.Name, modIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                entry = property.Value;
                return entry.ValueKind == JsonValueKind.Object;
            }
        }

        foreach (var property in data.EnumerateObject())
        {
            entry = property.Value;
            return entry.ValueKind == JsonValueKind.Object;
        }

        entry = default;
        return false;
    }

    private async Task<Result<ModReleaseInfo>> ResolveFromV1Async(
        string modIdentifier,
        string gameVersion,
        CancellationToken cancellationToken)
    {
        var details = await _modDb.GetModAsync(modIdentifier, cancellationToken).ConfigureAwait(false);
        if (!details.IsSuccess)
        {
            return Result<ModReleaseInfo>.Failure(details.Error ?? "Could not load mod details.");
        }

        var best = SelectBestRelease(details.Value!.Releases, gameVersion);
        if (best is null)
        {
            return Result<ModReleaseInfo>.Failure(
                $"No release of {modIdentifier} is tagged for game version {gameVersion}.");
        }

        return Result<ModReleaseInfo>.Success(best);
    }

    private string ResolveDownloadUrl(string? fileUrl, int fileId)
    {
        if (!string.IsNullOrWhiteSpace(fileUrl))
        {
            if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            if (fileUrl.StartsWith('/'))
            {
                var apiBase = new Uri(_endpoints.ModDbApiBaseUrl);
                var siteRoot = new Uri(apiBase.GetLeftPart(UriPartial.Authority) + "/");
                return new Uri(siteRoot, fileUrl).ToString();
            }
        }

        return _endpoints.BuildModDownloadUrl(fileId);
    }

    private static int? TryParseFileId(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return null;
        }

        var parts = fileUrl.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!string.Equals(parts[i], "download", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(parts[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                return id;
            }
        }

        return null;
    }

    private static HttpClient CreateDefaultClient()
        => new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
}
