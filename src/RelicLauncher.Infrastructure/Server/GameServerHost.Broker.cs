using System.Text;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Server;

public sealed partial class GameServerHost
{
    private async Task<Result> LaunchViaBrokerAsync(
        GameServerStartRequest request,
        string exe,
        string installPath,
        DotNetRuntimeResolveInfo runtime)
    {
        var dataPath = request.ServerDataPath.Trim();
        Directory.CreateDirectory(dataPath);

        var args = new List<string> { "--dataPath", dataPath };
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (runtime.IsManagedByRelic)
        {
            environment["DOTNET_ROOT"] = runtime.DotNetRoot;
        }

        var launch = await _broker.LaunchSandboxedAsync(
            new SandboxLaunchRequest
            {
                Kind = SandboxKind.DedicatedServer,
                ExecutablePath = exe,
                Arguments = args,
                Environment = environment,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? installPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            CancellationToken.None).ConfigureAwait(false);

        if (!launch.IsSuccess)
        {
            return Result.Failure(launch.Error ?? "Broker server launch failed.");
        }

        _brokerConsole.Attach(launch.Value!.ProcessId);
        _brokerProcessId = launch.Value.ProcessId;
        _lastRequest = request;
        RunningVersion = request.Version;
        _readCts = new CancellationTokenSource();
        _brokerPollTask = PollBrokerOutputAsync(_readCts.Token);
        AppendLine($"Started server {request.Version} (pid {launch.Value.ProcessId}, sandboxed={launch.Value.Sandboxed}).");
        SetState(ServerProcessState.Running);
        _logger.LogInformation(
            "Started sandboxed Vintage Story server {Version} pid {Pid}",
            request.Version,
            launch.Value.ProcessId);
        return Result.Success();
    }

    private async Task PollBrokerOutputAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var chunk = await _brokerConsole.ReadOutputAsync(cancellationToken).ConfigureAwait(false);
                if (chunk.Length == 0)
                {
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var text = Encoding.UTF8.GetString(chunk);
                foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    AppendLine(line.TrimEnd('\r'));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
