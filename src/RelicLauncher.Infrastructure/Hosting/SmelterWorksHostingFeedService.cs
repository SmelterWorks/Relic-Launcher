using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Hosting;

public sealed partial class SmelterWorksHostingFeedService : ISmelterWorksHostingFeedService, IDisposable
{
    private const string FeedUrl = "https://smelterworks.com/hosting/rss.xml";
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmelterWorksHostingFeedService> _logger;

    public SmelterWorksHostingFeedService(ILogger<SmelterWorksHostingFeedService> logger)
        : this(logger, CreateHttpClient())
    {
    }

    internal SmelterWorksHostingFeedService(ILogger<SmelterWorksHostingFeedService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public void Dispose() => _httpClient.Dispose();

    public async Task<Result<IReadOnlyList<HostingPlanInfo>>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(FeedUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SmelterWorks hosting feed returned {StatusCode}, using fallback plans", response.StatusCode);
                return Result<IReadOnlyList<HostingPlanInfo>>.Success(GetFallbackPlans());
            }

            var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var plans = SmelterWorksHostingFeedParser.Parse(xml);
            return Result<IReadOnlyList<HostingPlanInfo>>.Success(plans.Count > 0 ? plans : GetFallbackPlans());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogInformation(ex, "Could not load SmelterWorks hosting feed, using fallback plans");
            return Result<IReadOnlyList<HostingPlanInfo>>.Success(GetFallbackPlans());
        }
    }

    internal static IReadOnlyList<HostingPlanInfo> GetFallbackPlans()
        =>
        [
            new HostingPlanInfo
            {
                Name = "Ember",
                Subtitle = "Friends",
                MonthlyPrice = "$10 / month",
                AnnualPrice = "$100 / year",
                Highlights =
                [
                    "4 GB RAM",
                    "25 GB NVMe",
                    "Good for a small group on light or vanilla play",
                    "US or Germany",
                    "Docker export for self-hosting",
                ],
            },
            new HostingPlanInfo
            {
                Name = "Forge",
                Subtitle = "Modded",
                MonthlyPrice = "$15 / month",
                AnnualPrice = "$150 / year",
                Highlights =
                [
                    "8 GB RAM",
                    "50 GB NVMe",
                    "Good for normal ModDB packs and growing worlds",
                    "US or Germany",
                    "Docker export for self-hosting",
                ],
            },
            new HostingPlanInfo
            {
                Name = "Crucible",
                Subtitle = "Heavy",
                MonthlyPrice = "$25 / month",
                AnnualPrice = "$250 / year",
                Highlights =
                [
                    "16 GB RAM",
                    "100 GB NVMe",
                    "For big packs and busier worlds",
                    "US or Germany",
                    "Docker export for self-hosting",
                ],
            },
        ];

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RelicLauncher", "0.1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        return client;
    }
}
