using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Security;

namespace RelicLauncher.App.ViewModels;

public partial class ServersViewModel
{
    [ObservableProperty]
    private bool _isLanScanning;

    [ObservableProperty]
    private bool _hasLanResults;

    [ObservableProperty]
    private bool _showEmptyLan;

    [ObservableProperty]
    private string _lanEmptyMessage = string.Empty;

    public ObservableCollection<LanServerRowViewModel> LanResults { get; } = [];

    [RelayCommand]
    private async Task JoinLanServerAsync(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        LanValidationError = string.Empty;
        if (!ConnectAddressValidator.TryNormalize(address, out var normalized, out var error))
        {
            LanValidationError = error ?? ConnectAddressValidator.InvalidAddressMessage;
            return;
        }

        await JoinAddressAsync(normalized, null).ConfigureAwait(true);
    }

    private async Task ScanLanAsync()
    {
        var generation = Interlocked.Increment(ref _lanScanGeneration);
        IsLanScanning = true;
        try
        {
            var result = await _lanScanner.ScanAsync().ConfigureAwait(true);
            if (generation != _lanScanGeneration)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                LanResults.Clear();
                HasLanResults = false;
                ShowEmptyLan = true;
                LanEmptyMessage = result.Error ?? "Could not scan for LAN servers.";
                _logger.LogWarning("LAN scan failed: {Error}", result.Error);
                return;
            }

            LanResults.Clear();
            foreach (var server in result.Value!)
            {
                LanResults.Add(new LanServerRowViewModel(server));
            }

            HasLanResults = LanResults.Count > 0;
            ShowEmptyLan = !HasLanResults;
            LanEmptyMessage = HasLanResults
                ? string.Empty
                : "No LAN servers found. Start a world with Open to LAN or run a server on this network.";
        }
        finally
        {
            if (generation == _lanScanGeneration)
            {
                IsLanScanning = false;
            }
        }
    }
}
