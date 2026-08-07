using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

public sealed class WindowsSandboxLauncher
{
    private readonly ILogger<WindowsSandboxLauncher> _logger;
    private readonly WindowsAppContainerLauncher _appContainer;

    public WindowsSandboxLauncher(
        ILogger<WindowsSandboxLauncher> logger,
        WindowsAppContainerLauncher appContainer)
    {
        _logger = logger;
        _appContainer = appContainer;
    }

    public async Task<Result<SandboxLaunchResult>> LaunchAsync(
        SandboxPolicy policy,
        SandboxLaunchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var moniker = ResolveMoniker(policy.Kind);
        var result = await _appContainer.LaunchAsync(
            moniker,
            policy,
            request,
            cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            return result;
        }

        _logger.LogWarning(
            "AppContainer launch failed for {Kind}: {Error}. Falling back to unsandboxed.",
            policy.Kind,
            result.Error);

        return LaunchRawFallback(request, $"AppContainer failed: {result.Error}");
    }

    private static string ResolveMoniker(SandboxKind kind) =>
        kind switch
        {
            SandboxKind.Launcher => "SmelterWorks.RelicLauncher",
            SandboxKind.GameClient => "SmelterWorks.RelicLauncher.VSClient",
            SandboxKind.DedicatedServer => "SmelterWorks.RelicLauncher.VSServer",
            _ => "SmelterWorks.RelicLauncher",
        };

    private Result<SandboxLaunchResult> LaunchRawFallback(SandboxLaunchRequest request, string reason)
    {
        try
        {
            var startInfo = new global::System.Diagnostics.ProcessStartInfo
            {
                FileName = request.ExecutablePath,
                UseShellExecute = false,
                WorkingDirectory = request.WorkingDirectory
                    ?? Path.GetDirectoryName(request.ExecutablePath)
                    ?? Environment.CurrentDirectory,
                RedirectStandardInput = request.RedirectStandardInput,
                RedirectStandardOutput = request.RedirectStandardOutput,
                RedirectStandardError = request.RedirectStandardError,
            };

            foreach (var arg in request.Arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            foreach (var pair in request.Environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }

            var process = global::System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                return Result<SandboxLaunchResult>.Failure("Process did not start.");
            }

            return Result<SandboxLaunchResult>.Success(new SandboxLaunchResult
            {
                ProcessId = process.Id,
                Sandboxed = false,
                DegradedReason = reason,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                        or InvalidOperationException
                                        or IOException
                                        or UnauthorizedAccessException)
        {
            return Result<SandboxLaunchResult>.Failure(ex.Message);
        }
    }
}
