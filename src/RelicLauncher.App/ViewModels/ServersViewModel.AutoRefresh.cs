using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class ServersViewModel
{
    private static readonly TimeSpan CatalogAutoRefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LanAutoRefreshInterval = TimeSpan.FromSeconds(30);

    private CancellationTokenSource? _catalogAutoRefreshCts;
    private CancellationTokenSource? _lanAutoRefreshCts;
    private int _lanScanGeneration;

    private async Task StartCatalogAutoRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CatalogAutoRefreshInterval, cancellationToken).ConfigureAwait(true);
                await LoadCatalogAsync(forceNetwork: true).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void RestartCatalogAutoRefresh()
    {
        _catalogAutoRefreshCts?.Cancel();
        _catalogAutoRefreshCts = new CancellationTokenSource();
        _ = StartCatalogAutoRefreshLoopAsync(_catalogAutoRefreshCts.Token);
    }

    private async Task StartLanAutoRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ScanLanAsync().ConfigureAwait(true);
                await Task.Delay(LanAutoRefreshInterval, cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void RestartLanAutoRefresh()
    {
        _lanAutoRefreshCts?.Cancel();
        _lanAutoRefreshCts = new CancellationTokenSource();
        _ = StartLanAutoRefreshLoopAsync(_lanAutoRefreshCts.Token);
    }

    private void StopLanAutoRefresh()
    {
        _lanAutoRefreshCts?.Cancel();
        _lanAutoRefreshCts = null;
    }
}
