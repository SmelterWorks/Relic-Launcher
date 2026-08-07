using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Sandbox;

namespace RelicLauncher.Infrastructure.Paths;

public sealed class FileExplorerService : IFileExplorerService
{
    private readonly ISandboxBrokerClient _broker;
    private readonly ISandboxSupport _sandboxSupport;
    private readonly ILogger<FileExplorerService> _logger;

    public FileExplorerService(
        ISandboxBrokerClient broker,
        ISandboxSupport sandboxSupport,
        ILogger<FileExplorerService> logger)
    {
        _broker = broker;
        _sandboxSupport = sandboxSupport;
        _logger = logger;
    }

    public Result OpenFolder(string folderPath)
    {
        if (!PathValidator.TryGetFullPath(folderPath, out var fullPath, out var pathError))
        {
            return Result.Failure(pathError);
        }

        if (!Directory.Exists(fullPath))
        {
            return Result.Failure($"Folder does not exist: {fullPath}");
        }

        try
        {
            if (_sandboxSupport.IsBrokerConnected)
            {
                return _broker.OpenDirectoryAsync(fullPath).GetAwaiter().GetResult();
            }

            if (OperatingSystem.IsWindows())
            {
                global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo("explorer.exe", fullPath) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo("open", fullPath) { UseShellExecute = true });
            }
            else
            {
                global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo("xdg-open", fullPath) { UseShellExecute = true });
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _logger.LogWarning(ex, "Failed to open folder {Path}", fullPath);
            return Result.Failure(ex.Message);
        }
    }
}
