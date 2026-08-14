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
            FallbackPlan(
                "Ember",
                "Friends",
                "$10 / month",
                "$100 / year",
                "4 GB RAM",
                "25 GB NVMe",
                "Good for a small group on light or vanilla play",
                "US or Germany",
                "Docker export for self-hosting"),
            FallbackPlan(
                "Forge",
                "Modded",
                "$15 / month",
                "$150 / year",
                "8 GB RAM",
                "50 GB NVMe",
                "Good for normal ModDB packs and growing worlds",
                "US or Germany",
                "Docker export for self-hosting"),
            FallbackPlan(
                "Crucible",
                "Heavy",
                "$25 / month",
                "$250 / year",
                "16 GB RAM",
                "100 GB NVMe",
                "For big packs and busier worlds",
                "US or Germany",
                "Docker export for self-hosting"),
            FallbackPlan(
                "Anchor",
                "Bring Your Own Server",
                "$5 / month per daemon",
                "$50 / year",
                "Managed panel on your hardware",
                "Local backups included",
                "Optional cloud backups from $3/mo",
                "One-click migration"),
        ];

    private static HostingPlanInfo FallbackPlan(
        string name,
        string subtitle,
        string monthlyPrice,
        string annualPrice,
        params string[] highlights)
        => new()
        {
            Name = name,
            Subtitle = subtitle,
            MonthlyPrice = monthlyPrice,
            AnnualPrice = annualPrice,
            Highlights = highlights,
        };

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
