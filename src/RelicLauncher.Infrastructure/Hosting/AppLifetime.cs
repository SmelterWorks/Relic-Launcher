using RelicLauncher.Core.Abstractions;

namespace RelicLauncher.Infrastructure.Hosting;

public sealed class AppLifetime : IAppLifetime, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private Action? _shutdownRequested;

    public CancellationToken ApplicationStopping => _cts.Token;

    public void RegisterShutdownHandler(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _shutdownRequested = handler;
    }

    public void RequestShutdown()
    {
        try
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // Already disposed during shutdown.
        }

        _shutdownRequested?.Invoke();
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
