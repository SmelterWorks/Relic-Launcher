using System.ComponentModel;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Sandbox;

namespace RelicLauncher.Infrastructure.Paths;

public sealed class UrlLauncher : IUrlLauncher
{
    private readonly ISandboxBrokerClient _broker;
    private readonly ISandboxSupport _sandboxSupport;
    private readonly ILogger<UrlLauncher> _logger;

    public UrlLauncher(
        ISandboxBrokerClient broker,
        ISandboxSupport sandboxSupport,
        ILogger<UrlLauncher> logger)
    {
        _broker = broker;
        _sandboxSupport = sandboxSupport;
        _logger = logger;
    }

    public Result OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Result.Failure("URL is empty.");
        }

        try
        {
            if (_sandboxSupport.IsBrokerConnected)
            {
                return _broker.OpenUrlAsync(url).GetAwaiter().GetResult();
            }

            global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });
            return Result.Success();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to open URL {Url}", url);
            return Result.Failure(ex.Message);
        }
    }
}
