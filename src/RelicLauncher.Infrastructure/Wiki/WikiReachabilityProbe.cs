using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Wiki;

namespace RelicLauncher.Infrastructure.Wiki;

public sealed class WikiReachabilityProbe : IWikiReachabilityProbe
{
    private readonly ILogger<WikiReachabilityProbe> _logger;
    private readonly HttpClient _httpClient;
    private readonly IEndpointProvider _endpoints;

    public WikiReachabilityProbe(ILogger<WikiReachabilityProbe> logger, IEndpointProvider endpoints)
        : this(logger, CreateDefaultHttpClient(), endpoints)
    {
    }

    internal WikiReachabilityProbe(ILogger<WikiReachabilityProbe> logger, HttpClient httpClient, IEndpointProvider endpoints)
    {
        _logger = logger;
        _httpClient = httpClient;
        _endpoints = endpoints;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("RelicLauncher", GetUserAgentVersion()));
        }
    }

    public async Task<Result<WikiReachabilityResult>> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!WikiNavigationGuard.TryParseAbsoluteBase(_endpoints.WikiBaseUrl, out var wikiBase))
        {
            return Result<WikiReachabilityResult>.Failure("Wiki URL is not a valid http(s) address.");
        }

        var probeUri = new Uri(wikiBase, "api.php?action=query&meta=siteinfo&siprop=general&format=json&formatversion=2");

        try
        {
            using var response = await _httpClient.GetAsync(probeUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            var body = await ReadBodyPreviewAsync(response, cancellationToken).ConfigureAwait(false);
            return Result<WikiReachabilityResult>.Success(Classify(response, body));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogDebug(ex, "Wiki reachability probe failed for {Url}", probeUri);
            return Result<WikiReachabilityResult>.Success(new WikiReachabilityResult
            {
                Status = WikiReachabilityStatus.NetworkFailure,
                Detail = "Could not reach the wiki.",
            });
        }
    }

    internal static WikiReachabilityResult Classify(HttpResponseMessage response, string body)
    {
        var statusCode = (int)response.StatusCode;
        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (statusCode is 429 or 503)
        {
            return Failure(WikiReachabilityStatus.TemporarilyUnavailable, statusCode, $"Wiki returned HTTP {statusCode}.");
        }

        if (statusCode is 401 or 403 or 406
            || WikiChallengeDetector.LooksLikeChallenge(body, contentType)
            || ExpectsJsonButGotHtml(contentType, body))
        {
            return Failure(
                WikiReachabilityStatus.AccessBlocked,
                statusCode,
                statusCode >= 400
                    ? $"Wiki returned HTTP {statusCode}."
                    : "Wiki response looks like a bot challenge page.");
        }

        if (statusCode >= 500 || !response.IsSuccessStatusCode)
        {
            return Failure(WikiReachabilityStatus.ServerError, statusCode, $"Wiki returned HTTP {statusCode}.");
        }

        if (string.IsNullOrWhiteSpace(body)
            || (!body.Contains("\"query\"", StringComparison.Ordinal)
                && !body.Contains("\"general\"", StringComparison.Ordinal)))
        {
            return Failure(WikiReachabilityStatus.AccessBlocked, statusCode, "Wiki probe returned an unexpected response.");
        }

        return new WikiReachabilityResult
        {
            Status = WikiReachabilityStatus.Reachable,
            HttpStatusCode = statusCode,
        };
    }

    private static WikiReachabilityResult Failure(WikiReachabilityStatus status, int statusCode, string detail)
        => new()
        {
            Status = status,
            Detail = detail,
            HttpStatusCode = statusCode,
        };

    private static bool ExpectsJsonButGotHtml(string? contentType, string body)
    {
        if (!string.IsNullOrWhiteSpace(contentType)
            && contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = body.AsSpan().TrimStart();
        return trimmed.StartsWith("<!", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadBodyPreviewAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var buffer = new byte[8192];
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            return Encoding.UTF8.GetString(buffer, 0, read);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static HttpClient CreateDefaultHttpClient()
        => new(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };

    private static string GetUserAgentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
