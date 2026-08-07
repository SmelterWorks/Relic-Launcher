using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IGameServerHost
{
    ServerProcessState State { get; }

    IReadOnlyList<string> OutputLines { get; }

    string? RunningVersion { get; }

    event EventHandler? StateChanged;

    event EventHandler? OutputChanged;

    Task<Result> StartAsync(GameServerStartRequest request, CancellationToken cancellationToken = default);

    Task<Result> StopAsync(CancellationToken cancellationToken = default);

    Task<Result> RestartAsync(CancellationToken cancellationToken = default);

    Task<Result> SendCommandAsync(string command, CancellationToken cancellationToken = default);

    void ClearOutput();
}
