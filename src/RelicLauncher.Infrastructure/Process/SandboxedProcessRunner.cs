using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Process;

public sealed class SandboxedProcessRunner : IProcessRunner
{
    private readonly ISandboxBrokerClient _broker;
    private readonly ILogger<SandboxedProcessRunner> _logger;

    public SandboxedProcessRunner(ISandboxBrokerClient broker, ILogger<SandboxedProcessRunner> logger)
    {
        _broker = broker;
        _logger = logger;
    }

    public Task<Result> StartAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
        => StartAsync(executablePath, arguments, environment: null, cancellationToken);

    public async Task<Result> StartAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = SafeProcessRunner.Validate(executablePath, arguments);
        if (!validation.IsSuccess)
        {
            return Result.Failure(validation.Error!);
        }

        var env = environment is null
            ? new Dictionary<string, string?>(StringComparer.Ordinal)
            : new Dictionary<string, string?>(environment, StringComparer.Ordinal);

        var launch = await _broker.LaunchSandboxedAsync(
            new SandboxLaunchRequest
            {
                Kind = SandboxKind.GameClient,
                ExecutablePath = validation.Value!,
                Arguments = arguments?.ToList() ?? [],
                Environment = env,
                WorkingDirectory = Path.GetDirectoryName(validation.Value!),
            },
            cancellationToken).ConfigureAwait(false);

        if (!launch.IsSuccess)
        {
            return Result.Failure(launch.Error ?? "Sandbox launch failed.");
        }

        _logger.LogInformation(
            "Started sandboxed client pid {Pid} sandboxed={Sandboxed}",
            launch.Value!.ProcessId,
            launch.Value.Sandboxed);

        return Result.Success();
    }
}
