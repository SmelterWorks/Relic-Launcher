using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Server;

public sealed partial class GameServerHost
{
    private async Task<Result> StopCoreAsync(CancellationToken cancellationToken)
    {
        if (State is ServerProcessState.Stopped or ServerProcessState.Stopping)
        {
            return Result.Success();
        }

        var process = _process;
        if (process is null)
        {
            SetState(ServerProcessState.Stopped);
            RunningVersion = null;
            return Result.Success();
        }

        SetState(ServerProcessState.Stopping);
        AppendLine("Stopping server...");

        try
        {
            if (!process.HasExited)
            {
                await TryGracefulStopAsync(process, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning(ex, "Error while stopping server process");
        }
        finally
        {
            await CleanupProcessAsync().ConfigureAwait(false);
            SetState(ServerProcessState.Stopped);
            RunningVersion = null;
            AppendLine("Server stopped.");
        }

        return Result.Success();
    }

    private async Task TryGracefulStopAsync(global::System.Diagnostics.Process process, CancellationToken cancellationToken)
    {
        try
        {
            await process.StandardInput.WriteLineAsync("stop").ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _logger.LogDebug(ex, "Could not send stop command to server");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(StopGracePeriod);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task CleanupProcessAsync()
    {
        _readCts?.Cancel();
        await AwaitTaskSilentlyAsync(_stdoutTask).ConfigureAwait(false);
        await AwaitTaskSilentlyAsync(_stderrTask).ConfigureAwait(false);
        await AwaitTaskSilentlyAsync(_exitTask).ConfigureAwait(false);

        _readCts?.Dispose();
        _readCts = null;
        _process?.Dispose();
        _process = null;
        _stdoutTask = null;
        _stderrTask = null;
        _exitTask = null;
    }

    private static async Task AwaitTaskSilentlyAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    private async Task MonitorExitAsync(global::System.Diagnostics.Process process)
    {
        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
            AppendLine($"Server exited with code {process.ExitCode}.");
        }
        catch (Exception ex) when (ex is InvalidOperationException)
        {
            _logger.LogDebug(ex, "Server exit monitor ended");
        }
        finally
        {
            await _lifecycleLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (ReferenceEquals(_process, process))
                {
                    await CleanupProcessAsync().ConfigureAwait(false);
                    SetState(ServerProcessState.Stopped);
                    RunningVersion = null;
                }
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }
    }

    private async Task ReadStreamAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                AppendLine(line);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            _logger.LogDebug(ex, "Server output stream closed");
        }
    }
}
