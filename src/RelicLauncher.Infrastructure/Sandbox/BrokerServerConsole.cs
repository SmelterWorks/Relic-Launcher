using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Sandbox;

public sealed class BrokerServerConsole
{
    private readonly ISandboxBrokerClient _broker;
    private readonly ILogger<BrokerServerConsole> _logger;
    private int? _processId;

    public BrokerServerConsole(ISandboxBrokerClient broker, ILogger<BrokerServerConsole> logger)
    {
        _broker = broker;
        _logger = logger;
    }

    public int? ProcessId => _processId;

    public void Attach(int processId) => _processId = processId;

    public async Task<byte[]> ReadOutputAsync(CancellationToken cancellationToken = default)
    {
        if (_processId is null)
        {
            return [];
        }

        return await _broker.ReadProcessOutputAsync(_processId.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> WriteInputAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_processId is null)
        {
            return Result.Failure("No broker server process.");
        }

        return await _broker.WriteProcessInputAsync(_processId.Value, text, cancellationToken).ConfigureAwait(false);
    }

    public async Task KillAsync(CancellationToken cancellationToken = default)
    {
        if (_processId is null)
        {
            return;
        }

        await _broker.KillProcessAsync(_processId.Value, cancellationToken).ConfigureAwait(false);
        _processId = null;
    }
}
