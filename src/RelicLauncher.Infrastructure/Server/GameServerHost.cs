using System.Text;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

using RelicLauncher.Infrastructure.Sandbox;

namespace RelicLauncher.Infrastructure.Server;

public sealed partial class GameServerHost : IGameServerHost, IDisposable
{
    private const int MaxOutputLines = 2000;
    private static readonly TimeSpan StopGracePeriod = TimeSpan.FromSeconds(15);

    private readonly IDotNetRuntimeProvisioner _runtimeProvisioner;
    private readonly ISandboxBrokerClient _broker;
    private readonly ISandboxSupport _sandboxSupport;
    private readonly BrokerServerConsole _brokerConsole;
    private readonly ILogger<GameServerHost> _logger;
    private readonly Lock _gate = new();
    private readonly List<string> _outputLines = [];
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private global::System.Diagnostics.Process? _process;
    private GameServerStartRequest? _lastRequest;
    private CancellationTokenSource? _readCts;
    private Task? _stdoutTask;
    private Task? _stderrTask;
    private Task? _exitTask;
    private Task? _brokerPollTask;
    private int? _brokerProcessId;

    public GameServerHost(
        IDotNetRuntimeProvisioner runtimeProvisioner,
        ISandboxBrokerClient broker,
        ISandboxSupport sandboxSupport,
        BrokerServerConsole brokerConsole,
        ILogger<GameServerHost> logger)
    {
        _runtimeProvisioner = runtimeProvisioner;
        _broker = broker;
        _sandboxSupport = sandboxSupport;
        _brokerConsole = brokerConsole;
        _logger = logger;
    }

    public ServerProcessState State { get; private set; } = ServerProcessState.Stopped;

    public IReadOnlyList<string> OutputLines
    {
        get
        {
            lock (_gate)
            {
                return _outputLines.ToArray();
            }
        }
    }

    public string? RunningVersion { get; private set; }

    public event EventHandler? StateChanged;

    public event EventHandler? OutputChanged;

    public async Task<Result> StartAsync(GameServerStartRequest request, CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await StartCoreAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<Result> StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await StopCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<Result> RestartAsync(CancellationToken cancellationToken = default)
    {
        var request = _lastRequest;
        if (request is null)
        {
            return Result.Failure("No server has been started yet.");
        }

        var stop = await StopAsync(cancellationToken).ConfigureAwait(false);
        if (!stop.IsSuccess)
        {
            return stop;
        }

        return await StartAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State != ServerProcessState.Running)
        {
            return Result.Failure("Server is not running.");
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return Result.Failure("Command is empty.");
        }

        if (_brokerProcessId is not null)
        {
            var line = command.EndsWith('\n') ? command : command + Environment.NewLine;
            var write = await _brokerConsole.WriteInputAsync(line, cancellationToken).ConfigureAwait(false);
            if (write.IsSuccess)
            {
                AppendLine($"> {command}");
            }

            return write;
        }

        if (_process is null)
        {
            return Result.Failure("Server is not running.");
        }

        try
        {
            var writer = _process.StandardInput;
            writer.WriteLine(command);
            writer.Flush();
            AppendLine($"> {command}");
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return Result.Failure(ex.Message);
        }
    }

    public void ClearOutput()
    {
        lock (_gate)
        {
            _outputLines.Clear();
        }

        OutputChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        try
        {
            StopCoreAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error stopping server during dispose");
        }

        _lifecycleLock.Dispose();
        _readCts?.Dispose();
    }

    private void AppendLine(string line)
    {
        lock (_gate)
        {
            _outputLines.Add(line);
            if (_outputLines.Count > MaxOutputLines)
            {
                _outputLines.RemoveRange(0, _outputLines.Count - MaxOutputLines);
            }
        }

        OutputChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetState(ServerProcessState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
