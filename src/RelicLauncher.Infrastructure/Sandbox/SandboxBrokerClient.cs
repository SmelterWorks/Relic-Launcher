using System.Text;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

public sealed class SandboxBrokerClient : ISandboxBrokerClient
{
    private readonly PassthroughSandboxBrokerClient _fallback;
    private readonly ILogger<SandboxBrokerClient> _logger;
    private readonly string? _socketPath;
    private readonly string? _pipeName;

    public SandboxBrokerClient(
        PassthroughSandboxBrokerClient fallback,
        ILogger<SandboxBrokerClient> logger)
    {
        _fallback = fallback;
        _logger = logger;
        _socketPath = Environment.GetEnvironmentVariable(SandboxEnvironment.BrokerSocketPath);
        _pipeName = Environment.GetEnvironmentVariable(SandboxEnvironment.BrokerPipeName);
    }

    public async Task<Result<SandboxLaunchResult>> LaunchSandboxedAsync(
        SandboxLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsBrokerConnected())
        {
            return await _fallback.LaunchSandboxedAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var policyJson = request.PolicyOverride is not null
            ? SandboxPolicyJson.Serialize(request.PolicyOverride)
            : null;

        var response = await SendAsync(new BrokerRequest
        {
            Kind = BrokerRequestKind.LaunchSandboxed,
            Launch = new BrokerLaunchPayload
            {
                SandboxKind = (int)request.Kind,
                Executable = request.ExecutablePath,
                Arguments = request.Arguments.ToList(),
                Environment = new Dictionary<string, string?>(request.Environment, StringComparer.Ordinal),
                WorkingDirectory = request.WorkingDirectory,
                RedirectStandardInput = request.RedirectStandardInput,
                RedirectStandardOutput = request.RedirectStandardOutput,
                RedirectStandardError = request.RedirectStandardError,
                PolicyJson = policyJson,
            },
        }, cancellationToken).ConfigureAwait(false);

        if (!response.Ok)
        {
            return Result<SandboxLaunchResult>.Failure(response.Error ?? "Broker launch failed.");
        }

        return Result<SandboxLaunchResult>.Success(new SandboxLaunchResult
        {
            ProcessId = response.ProcessId ?? 0,
            Sandboxed = response.Sandboxed,
            DegradedReason = response.DegradedReason,
        });
    }

    public async Task<Result> OpenDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (!IsBrokerConnected())
        {
            return await _fallback.OpenDirectoryAsync(directoryPath, cancellationToken).ConfigureAwait(false);
        }

        var response = await SendAsync(new BrokerRequest
        {
            Kind = BrokerRequestKind.OpenDirectory,
            Path = directoryPath,
        }, cancellationToken).ConfigureAwait(false);

        return response.Ok ? Result.Success() : Result.Failure(response.Error ?? "Broker open directory failed.");
    }

    public async Task<Result> OpenUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!IsBrokerConnected())
        {
            return await _fallback.OpenUrlAsync(url, cancellationToken).ConfigureAwait(false);
        }

        var response = await SendAsync(new BrokerRequest
        {
            Kind = BrokerRequestKind.OpenUrl,
            Url = url,
        }, cancellationToken).ConfigureAwait(false);

        return response.Ok ? Result.Success() : Result.Failure(response.Error ?? "Broker open url failed.");
    }

    public async Task<Result> RunInstallerAsync(
        string installerPath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        if (!IsBrokerConnected())
        {
            return await _fallback.RunInstallerAsync(installerPath, arguments, cancellationToken).ConfigureAwait(false);
        }

        var response = await SendAsync(new BrokerRequest
        {
            Kind = BrokerRequestKind.RunInstaller,
            Installer = new BrokerInstallerPayload
            {
                Executable = installerPath,
                Arguments = arguments.ToList(),
            },
        }, cancellationToken).ConfigureAwait(false);

        return response.Ok ? Result.Success() : Result.Failure(response.Error ?? "Broker installer failed.");
    }

    public async Task<Result> WriteFileAsync(
        string destinationPath,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (!IsBrokerConnected())
        {
            return await _fallback.WriteFileAsync(destinationPath, content, cancellationToken).ConfigureAwait(false);
        }

        var response = await SendAsync(new BrokerRequest
        {
            Kind = BrokerRequestKind.WriteFile,
            WriteFile = new BrokerWriteFilePayload
            {
                Path = destinationPath,
                Base64 = Convert.ToBase64String(content),
            },
        }, cancellationToken).ConfigureAwait(false);

        return response.Ok ? Result.Success() : Result.Failure(response.Error ?? "Broker write failed.");
    }

    public async Task<byte[]> ReadProcessOutputAsync(int processId, CancellationToken cancellationToken = default)
    {
        if (!IsBrokerConnected())
        {
            return [];
        }

        var response = await SendAsync(new BrokerRequest
        {
            Kind = BrokerRequestKind.ReadProcessOutput,
            ProcessId = processId,
        }, cancellationToken).ConfigureAwait(false);

        if (!response.Ok || string.IsNullOrEmpty(response.OutputBase64))
        {
            return [];
        }

        return Convert.FromBase64String(response.OutputBase64);
    }

    public async Task<Result> WriteProcessInputAsync(
        int processId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!IsBrokerConnected())
        {
            return Result.Failure("Broker is not connected.");
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        var response = await SendAsync(new BrokerRequest
        {
            Kind = BrokerRequestKind.WriteProcessInput,
            ProcessId = processId,
            InputBase64 = Convert.ToBase64String(bytes),
        }, cancellationToken).ConfigureAwait(false);

        return response.Ok ? Result.Success() : Result.Failure(response.Error ?? "Broker write input failed.");
    }

    public async Task<Result> KillProcessAsync(int processId, CancellationToken cancellationToken = default)
    {
        if (!IsBrokerConnected())
        {
            return Result.Failure("Broker is not connected.");
        }

        var response = await SendAsync(new BrokerRequest
        {
            Kind = BrokerRequestKind.KillProcess,
            ProcessId = processId,
        }, cancellationToken).ConfigureAwait(false);

        return response.Ok ? Result.Success() : Result.Failure(response.Error ?? "Broker kill failed.");
    }

    private bool IsBrokerConnected() =>
        !string.IsNullOrEmpty(_socketPath) || !string.IsNullOrEmpty(_pipeName);

    private async Task<BrokerResponse> SendAsync(BrokerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var transport = _socketPath is not null
                ? await BrokerPipeTransport.ConnectUnixSocketAsync(_socketPath, cancellationToken).ConfigureAwait(false)
                : await BrokerPipeTransport.ConnectNamedPipeAsync(_pipeName!, cancellationToken).ConfigureAwait(false);

            return await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or EndOfStreamException)
        {
            _logger.LogWarning(ex, "Broker IPC failed");
            return new BrokerResponse { Ok = false, Error = ex.Message };
        }
    }
}
