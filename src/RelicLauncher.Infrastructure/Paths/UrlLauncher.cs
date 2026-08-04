using System.ComponentModel;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Paths;

public sealed class UrlLauncher : IUrlLauncher
{
    private readonly ILogger<UrlLauncher> _logger;

    public UrlLauncher(ILogger<UrlLauncher> logger)
    {
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
