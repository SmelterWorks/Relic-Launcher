using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Testing;

public sealed class TestSandboxBrokerClient : ISandboxBrokerClient
{
    public Task<Result<SandboxLaunchResult>> LaunchSandboxedAsync(
        SandboxLaunchRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<SandboxLaunchResult>.Success(new SandboxLaunchResult
        {
            ProcessId = Environment.ProcessId,
            Sandboxed = false,
        }));

    public Task<Result> OpenDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());

    public Task<Result> OpenUrlAsync(string url, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());

    public Task<Result> RunInstallerAsync(
        string installerPath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());

    public Task<Result> WriteFileAsync(
        string destinationPath,
        byte[] content,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());

    public Task<byte[]> ReadProcessOutputAsync(int processId, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<byte>());

    public Task<Result> WriteProcessInputAsync(
        int processId,
        string text,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());

    public Task<Result> KillProcessAsync(int processId, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());
}
