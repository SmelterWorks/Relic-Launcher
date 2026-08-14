using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Panel;

public sealed class SmelterWorksPanelClient : ISmelterWorksPanelClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IEndpointProvider _endpoints;
    private readonly ILogger<SmelterWorksPanelClient> _logger;

    public SmelterWorksPanelClient(IEndpointProvider endpoints, ILogger<SmelterWorksPanelClient> logger)
        : this(endpoints, logger, new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
    {
    }

    internal SmelterWorksPanelClient(IEndpointProvider endpoints, ILogger<SmelterWorksPanelClient> logger, HttpClient httpClient)
    {
        _endpoints = endpoints;
        _logger = logger;
        _httpClient = httpClient;
    }

    public void Dispose() => _httpClient.Dispose();

    public async Task<Result<IReadOnlyList<PanelServerSummary>>> GetMyServersAsync(string apiToken, CancellationToken cancellationToken = default)
    {
        return await GetListAsync<PanelServerSummary>(
            "/api/v1/relic/servers",
            apiToken,
            "servers",
            static (JsonElement item) => new PanelServerSummary
            {
                Uuid = item.GetProperty("uuid").GetString() ?? string.Empty,
                Name = item.GetProperty("name").GetString() ?? string.Empty,
                Type = item.GetProperty("type").GetString() ?? string.Empty,
                Status = item.GetProperty("status").GetString() ?? string.Empty,
                ConnectAddress = item.TryGetProperty("connect_address", out var address) ? address.GetString() : null,
                DaemonOnline = item.TryGetProperty("daemon_online", out var online) && online.GetBoolean(),
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<MigrationJobSummary>>> GetMigrationsAsync(string apiToken, CancellationToken cancellationToken = default)
    {
        return await GetListAsync<MigrationJobSummary>(
            "/api/v1/relic/migrations",
            apiToken,
            "migrations",
            static (JsonElement item) => new MigrationJobSummary
            {
                Uuid = item.GetProperty("uuid").GetString() ?? string.Empty,
                Status = item.GetProperty("status").GetString() ?? string.Empty,
                Bytes = item.TryGetProperty("bytes", out var bytes) ? bytes.GetInt64() : 0,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<IReadOnlyList<T>>> GetListAsync<T>(
        string path,
        string apiToken,
        string property,
        Func<JsonElement, T> map,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<T>>.Failure($"Panel API returned {(int)response.StatusCode}");
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!document.RootElement.TryGetProperty(property, out var items) || items.ValueKind != JsonValueKind.Array)
                {
                    return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());
                }

                var list = new List<T>();
                foreach (var item in items.EnumerateArray())
                {
                    list.Add(map(item));
                }

                return Result<IReadOnlyList<T>>.Success(list);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogInformation(ex, "SmelterWorks panel request failed");
            return Result<IReadOnlyList<T>>.Failure(ex.Message);
        }
    }

    private string BuildUrl(string path)
    {
        var baseUrl = _endpoints.PanelApiBaseUrl.TrimEnd('/');
        return $"{baseUrl}{path}";
    }
}
