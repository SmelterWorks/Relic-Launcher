using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Launch;

public sealed partial class GameLaunchService
{
    public async Task<Result> StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsRunning || _processId is null)
            {
                return Result.Success();
            }

            _isStopping = true;
            NotifyStateChanged();

            var processId = _processId.Value;
            try
            {
                var kill = await _broker.KillProcessAsync(processId, cancellationToken).ConfigureAwait(false);
                if (!kill.IsSuccess)
                {
                    TryKillProcess(processId);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                _logger.LogWarning(ex, "Failed to stop game client pid {Pid}", processId);
                TryKillProcess(processId);
            }
            finally
            {
                ClearRunningState();
            }

            return Result.Success();
        }
        finally
        {
            _isStopping = false;
            _lifecycleLock.Release();
        }
    }

    private void AttachProcess(int processId, string version)
    {
        _processId = processId;
        _runningVersion = version;
        IsRunning = true;
        _exitMonitorTask = MonitorExitAsync(processId);
        NotifyStateChanged();
        _logger.LogInformation("Started Vintage Story client {Version} pid {Pid}", version, processId);
    }

    private async Task MonitorExitAsync(int processId)
    {
        try
        {
            using var process = global::System.Diagnostics.Process.GetProcessById(processId);
            await process.WaitForExitAsync().ConfigureAwait(false);
            _logger.LogInformation("Vintage Story client pid {Pid} exited with code {Code}", processId, process.ExitCode);
        }
        catch (ArgumentException)
        {
            _logger.LogDebug("Vintage Story client pid {Pid} already exited", processId);
        }
        catch (Exception ex) when (ex is InvalidOperationException)
        {
            _logger.LogDebug(ex, "Vintage Story client exit monitor ended for pid {Pid}", processId);
        }
        finally
        {
            await _lifecycleLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_processId == processId)
                {
                    ClearRunningState();
                }
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }
    }

    private void ClearRunningState()
    {
        _processId = null;
        _runningVersion = null;
        IsRunning = false;
        _exitMonitorTask = null;
        NotifyStateChanged();
    }

    private static void TryKillProcess(int processId)
    {
        try
        {
            using var process = global::System.Diagnostics.Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (ArgumentException)
        {
        }
        catch (Exception)
        {
        }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
