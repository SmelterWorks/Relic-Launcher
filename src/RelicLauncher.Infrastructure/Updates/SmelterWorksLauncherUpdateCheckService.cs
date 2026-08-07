using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Updates;

public sealed class SmelterWorksLauncherUpdateCheckService : IUpdateCheckService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmelterWorksLauncherUpdateCheckService> _logger;

    public SmelterWorksLauncherUpdateCheckService(ILogger<SmelterWorksLauncherUpdateCheckService> logger)
        : this(logger, CreateHttpClient())
    {
    }

    internal SmelterWorksLauncherUpdateCheckService(ILogger<SmelterWorksLauncherUpdateCheckService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<Result<LauncherUpdateCheckResult>> CheckForLauncherUpdateAsync(
        LauncherUpdateCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        var manifestUrl = BuildManifestUrl(request.Channel);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
        if (!string.IsNullOrWhiteSpace(request.IfNoneMatchEtag))
        {
            httpRequest.Headers.TryAddWithoutValidation("If-None-Match", request.IfNoneMatchEtag);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "Launcher update check failed for {Url}", manifestUrl);
            return Result<LauncherUpdateCheckResult>.Failure("Could not reach the update server.");
        }

        using (response)
        {
            return await HandleResponseAsync(response, request, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose() => _httpClient.Dispose();

    private async Task<Result<LauncherUpdateCheckResult>> HandleResponseAsync(
        HttpResponseMessage response,
        LauncherUpdateCheckRequest request,
        CancellationToken cancellationToken)
    {
        var etag = response.Headers.ETag?.Tag?.Trim('"');

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return Result<LauncherUpdateCheckResult>.Success(new LauncherUpdateCheckResult
            {
                NotModified = true,
                Etag = etag ?? request.IfNoneMatchEtag,
            });
        }

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogDebug("Launcher update manifest unavailable: {Status}", response.StatusCode);
            return Result<LauncherUpdateCheckResult>.Success(new LauncherUpdateCheckResult { Etag = etag });
        }

        if (!response.IsSuccessStatusCode)
        {
            return Result<LauncherUpdateCheckResult>.Failure($"Update server returned {(int)response.StatusCode}.");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var parsed = UpdateManifestParser.Parse(json, request.Channel);
        if (!parsed.IsSuccess)
        {
            return Result<LauncherUpdateCheckResult>.Failure(parsed.Error ?? "Invalid update manifest.");
        }

        if (!LauncherVersionComparer.IsUpdateAvailable(BuildMetadata.Version, parsed.Value!.Version))
        {
            return Result<LauncherUpdateCheckResult>.Success(new LauncherUpdateCheckResult { Etag = etag });
        }

        return Result<LauncherUpdateCheckResult>.Success(new LauncherUpdateCheckResult
        {
            Update = parsed.Value,
            Etag = etag,
        });
    }

    private static string BuildManifestUrl(LauncherUpdateChannel channel)
    {
        var suffix = channel == LauncherUpdateChannel.Nightly ? "nightly" : "stable";
        return $"{RelicLauncherEndpoints.UpdatesBaseUrl}/{suffix}.json";
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = false,
        })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RelicLauncher", BuildMetadata.Version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
