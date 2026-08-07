using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RelicLauncher.Core;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.SelfCheck;
using RelicLauncher.Infrastructure.Endpoints;
using RelicLauncher.Infrastructure.Versions;

namespace RelicLauncher.Infrastructure.SelfCheck;

internal static class SelfCheckCatalogProbe
{
    private const int MaxAttempts = 3;

    internal static Func<HttpClient>? HttpClientFactoryForTests { get; set; }

    public static async Task<SelfCheckItem> RunAsync(
        Func<ServiceProvider> serviceProviderFactory,
        CancellationToken cancellationToken)
    {
        string? lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var provider = serviceProviderFactory();
                var catalog = provider.GetRequiredService<IGameVersionCatalog>();
                var versions = await catalog.GetVersionsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                if (versions.IsSuccess && versions.Value!.Count > 0)
                {
                    var latest = await catalog.GetLatestStableVersionAsync(cancellationToken).ConfigureAwait(false);
                    var detail = latest.IsSuccess && !string.IsNullOrWhiteSpace(latest.Value)
                        ? $"{versions.Value.Count} versions; latest stable {latest.Value}"
                        : $"{versions.Value.Count} versions";
                    if (attempt > 1)
                    {
                        detail += $" (attempt {attempt})";
                    }

                    return SelfCheckItem.Pass("catalog", "Version catalog", detail);
                }

                lastError = versions.Error ?? "No versions returned";
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
            }
        }

        var direct = await TryDirectFetchAsync(cancellationToken).ConfigureAwait(false);
        if (direct is not null)
        {
            return direct;
        }

        return SelfCheckItem.Fail(
            "catalog",
            "Version catalog",
            $"{lastError} (after {MaxAttempts} attempts)");
    }

    private static async Task<SelfCheckItem?> TryDirectFetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateHttpClient();
            var endpoints = new EndpointProvider();
            var json = await client.GetStringAsync(endpoints.VersionCatalogUrl, cancellationToken).ConfigureAwait(false);
            var versions = VintageStoryVersionCatalog.ParseCatalog(json);
            if (versions.Count == 0)
            {
                return null;
            }

            var latestText = await client.GetStringAsync(endpoints.LatestStableUrl, cancellationToken).ConfigureAwait(false);
            var latest = latestText.Trim();
            var detail = string.IsNullOrWhiteSpace(latest)
                ? $"{versions.Count} versions (direct fetch)"
                : $"{versions.Count} versions; latest stable {latest} (direct fetch)";
            return SelfCheckItem.Pass("catalog", "Version catalog", detail);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        if (HttpClientFactoryForTests is not null)
        {
            return HttpClientFactoryForTests();
        }

        var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(90),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"RelicLauncher/{BuildMetadata.Version}");
        return client;
    }
}
