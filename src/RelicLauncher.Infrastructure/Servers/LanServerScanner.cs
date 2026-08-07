using System.Globalization;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Server;
using RelicLauncher.Infrastructure.Server;

namespace RelicLauncher.Infrastructure.Servers;

public sealed class LanServerScanner : ILanServerScanner
{
    private const int DefaultPort = VintageStoryServerConfigReader.DefaultPort;
    private const int ConnectTimeoutMs = 400;
    private const int QueryTimeoutMs = 1200;
    private const int MaxParallelProbes = 48;
    private const int MaxAddressesPerScan = 512;

    private readonly IGameServerHost _serverHost;
    private readonly IRuntimePlatform _platform;
    private readonly ILogger<LanServerScanner> _logger;

    public LanServerScanner(
        IGameServerHost serverHost,
        IRuntimePlatform platform,
        ILogger<LanServerScanner> logger)
    {
        _serverHost = serverHost;
        _platform = platform;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<LanServerSummary>>> ScanAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var candidates = BuildCandidateAddresses();
            var openEndpoints = await ProbeOpenEndpointsAsync(candidates, cancellationToken).ConfigureAwait(false);
            using var gate = new SemaphoreSlim(MaxParallelProbes, MaxParallelProbes);

            var queryTasks = openEndpoints.Select(async endpoint =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await QueryEndpointAsync(endpoint, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    gate.Release();
                }
            });

            var summaries = (await Task.WhenAll(queryTasks).ConfigureAwait(false))
                .Where(summary => summary is not null)
                .Select(summary => summary!)
                .OrderBy(s => s.ServerName ?? s.Address, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Result<IReadOnlyList<LanServerSummary>>.Success(summaries);
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            _logger.LogDebug(ex, "LAN server scan failed");
            return Result<IReadOnlyList<LanServerSummary>>.Failure(ex.Message);
        }
    }

    private List<string> BuildCandidateAddresses()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"127.0.0.1:{DefaultPort}",
        };

        foreach (var address in LanSubnetAddressEnumerator.Collect(DefaultPort, MaxAddressesPerScan))
        {
            candidates.Add(address);
            if (candidates.Count >= MaxAddressesPerScan)
            {
                break;
            }
        }

        if (_serverHost.State == ServerProcessState.Running)
        {
            var dataPath = _platform.GetPlatformInfo().DefaultServerDataPath;
            foreach (var endpoint in ServerListenEndpointResolver.Resolve(dataPath))
            {
                candidates.Add(endpoint);
            }
        }

        return candidates.Take(MaxAddressesPerScan).ToList();
    }

    private static async Task<List<string>> ProbeOpenEndpointsAsync(
        IReadOnlyList<string> candidates,
        CancellationToken cancellationToken)
    {
        var open = new List<string>();
        using var gate = new SemaphoreSlim(MaxParallelProbes, MaxParallelProbes);

        var tasks = candidates.Select(async candidate =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (await IsPortOpenAsync(candidate, cancellationToken).ConfigureAwait(false))
                {
                    lock (open)
                    {
                        open.Add(candidate);
                    }
                }
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return open;
    }

    private static async Task<bool> IsPortOpenAsync(string address, CancellationToken cancellationToken)
    {
        if (!TrySplitAddress(address, out var host, out var port))
        {
            return false;
        }

        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ConnectTimeoutMs);
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
        {
            return false;
        }
    }

    private async Task<LanServerSummary?> QueryEndpointAsync(string address, CancellationToken cancellationToken)
    {
        if (!TrySplitAddress(address, out var host, out var port))
        {
            return null;
        }

        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(QueryTimeoutMs);
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);

            var stream = client.GetStream();
            await stream.WriteAsync(VintageStoryLanQueryProtocol.QueryPacket, timeout.Token).ConfigureAwait(false);

            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), timeout.Token).ConfigureAwait(false);
            if (read <= 0)
            {
                return CreateFallbackSummary(address);
            }

            if (!VintageStoryLanQueryProtocol.TryParseQueryAnswer(buffer.AsSpan(0, read), out var answer))
            {
                return CreateFallbackSummary(address);
            }

            return new LanServerSummary
            {
                Address = address,
                ServerName = answer.Name ?? address,
                Players = answer.PlayerCount,
                MaxPlayers = answer.MaxPlayers,
                GameVersion = answer.ServerVersion,
                HasPassword = answer.HasPassword,
                Description = answer.Motd,
                IsLocalHosted = IsRelicHostedEndpoint(address),
            };
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            _logger.LogDebug(ex, "LAN query failed for {Address}", address);
            return CreateFallbackSummary(address);
        }
    }

    private bool IsRelicHostedEndpoint(string address)
    {
        if (_serverHost.State != ServerProcessState.Running)
        {
            return false;
        }

        var dataPath = _platform.GetPlatformInfo().DefaultServerDataPath;
        return ServerListenEndpointResolver.Resolve(dataPath)
            .Any(endpoint => string.Equals(endpoint, address, StringComparison.OrdinalIgnoreCase));
    }

    private static LanServerSummary CreateFallbackSummary(string address)
    {
        return new LanServerSummary
        {
            Address = address,
            ServerName = address,
            Players = 0,
            MaxPlayers = 0,
            HasPassword = false,
            IsLocalHosted = false,
        };
    }

    private static bool TrySplitAddress(string address, out string host, out int port)
    {
        host = string.Empty;
        port = DefaultPort;
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var trimmed = address.Trim();
        var colon = trimmed.LastIndexOf(':');
        if (colon <= 0 || colon >= trimmed.Length - 1)
        {
            host = trimmed;
            return true;
        }

        host = trimmed[..colon];
        if (host.StartsWith('[') && host.EndsWith(']'))
        {
            host = host[1..^1];
        }

        return int.TryParse(trimmed[(colon + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out port)
            && port is > 0 and <= 65535;
    }
}
