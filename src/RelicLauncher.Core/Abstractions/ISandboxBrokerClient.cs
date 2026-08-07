using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Core.Abstractions;

public interface ISandboxBrokerClient
{
    Task<Result<SandboxLaunchResult>> LaunchSandboxedAsync(
        SandboxLaunchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> OpenDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);

    Task<Result> OpenUrlAsync(string url, CancellationToken cancellationToken = default);

    Task<Result> RunInstallerAsync(
        string installerPath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);

    Task<Result> WriteFileAsync(
        string destinationPath,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task<byte[]> ReadProcessOutputAsync(int processId, CancellationToken cancellationToken = default);

    Task<Result> WriteProcessInputAsync(
        int processId,
        string text,
        CancellationToken cancellationToken = default);

    Task<Result> KillProcessAsync(int processId, CancellationToken cancellationToken = default);
}
