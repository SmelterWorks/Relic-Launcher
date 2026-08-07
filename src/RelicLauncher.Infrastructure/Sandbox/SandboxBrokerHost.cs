using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;
using RelicLauncher.Core.Server;

namespace RelicLauncher.Infrastructure.Sandbox;

public sealed partial class SandboxBrokerHost : IAsyncDisposable, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly PassthroughSandboxBrokerClient _launcher;
    private readonly ILauncherSettingsStore _settingsStore;
    private readonly IRuntimePlatform _platform;
    private readonly IAppPathProvider _pathProvider;
    private readonly ILogger<SandboxBrokerHost> _logger;
    private readonly ConcurrentDictionary<int, BrokerManagedProcess> _processes = new();
    private CancellationTokenSource? _cts;

    public SandboxBrokerHost(
        PassthroughSandboxBrokerClient launcher,
        ILauncherSettingsStore settingsStore,
        IRuntimePlatform platform,
        IAppPathProvider pathProvider,
        ILogger<SandboxBrokerHost> logger)
    {
        _launcher = launcher;
        _settingsStore = settingsStore;
        _platform = platform;
        _pathProvider = pathProvider;
        _logger = logger;
    }

    public async Task<int> RunAsync(string socketPath, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var socketDir = Path.GetDirectoryName(socketPath)!;
        Directory.CreateDirectory(socketDir);
        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
        }

        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        listener.Listen(8);

        _logger.LogInformation("Sandbox broker listening on {Socket}", socketPath);

        while (!_cts.Token.IsCancellationRequested)
        {
            var client = await listener.AcceptAsync(_cts.Token).ConfigureAwait(false);
            _ = HandleClientAsync(client, _cts.Token);
        }

        return 0;
    }

    private async Task HandleClientAsync(Socket client, CancellationToken cancellationToken)
    {
        using var stream = new NetworkStream(client, ownsSocket: true);
        var transport = BrokerPipeTransport.FromStream(stream, _logger);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var lengthBuf = new byte[4];
                await ReadExactAsync(stream, lengthBuf, cancellationToken).ConfigureAwait(false);
                var length = BitConverter.ToInt32(lengthBuf, 0);
                if (length <= 0 || length > 16 * 1024 * 1024)
                {
                    break;
                }

                var payload = new byte[length];
                await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);
                var request = JsonSerializer.Deserialize<BrokerRequest>(payload, JsonOptions)
                    ?? new BrokerRequest { Kind = BrokerRequestKind.Ping };

                var response = await HandleRequestAsync(request, cancellationToken).ConfigureAwait(false);
                var responseJson = JsonSerializer.Serialize(response, JsonOptions);
                var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                var responseLength = BitConverter.GetBytes(responseBytes.Length);
                await stream.WriteAsync(responseLength, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or OperationCanceledException)
        {
            _logger.LogDebug(ex, "Broker client disconnected");
        }
    }

    private async Task<BrokerResponse> HandleRequestAsync(BrokerRequest request, CancellationToken cancellationToken)
    {
        switch (request.Kind)
        {
            case BrokerRequestKind.LaunchSandboxed:
                return await HandleLaunchAsync(request.Launch!, cancellationToken).ConfigureAwait(false);
            case BrokerRequestKind.OpenDirectory:
                var openDir = await _launcher.OpenDirectoryAsync(request.Path!, cancellationToken).ConfigureAwait(false);
                return openDir.IsSuccess
                    ? new BrokerResponse { Ok = true }
                    : new BrokerResponse { Ok = false, Error = openDir.Error };
            case BrokerRequestKind.OpenUrl:
                var openUrl = await _launcher.OpenUrlAsync(request.Url!, cancellationToken).ConfigureAwait(false);
                return openUrl.IsSuccess
                    ? new BrokerResponse { Ok = true }
                    : new BrokerResponse { Ok = false, Error = openUrl.Error };
            case BrokerRequestKind.RunInstaller:
                var installer = await _launcher.RunInstallerAsync(
                    request.Installer!.Executable,
                    request.Installer.Arguments,
                    cancellationToken).ConfigureAwait(false);
                return installer.IsSuccess
                    ? new BrokerResponse { Ok = true }
                    : new BrokerResponse { Ok = false, Error = installer.Error };
            case BrokerRequestKind.WriteFile:
                var write = await _launcher.WriteFileAsync(
                    request.WriteFile!.Path,
                    Convert.FromBase64String(request.WriteFile.Base64),
                    cancellationToken).ConfigureAwait(false);
                return write.IsSuccess
                    ? new BrokerResponse { Ok = true }
                    : new BrokerResponse { Ok = false, Error = write.Error };
            case BrokerRequestKind.ReadProcessOutput:
                return ReadProcessOutput(request);
            case BrokerRequestKind.WriteProcessInput:
                return WriteProcessInput(request);
            case BrokerRequestKind.KillProcess:
                return KillProcess(request);
            default:
                return new BrokerResponse { Ok = true };
        }
    }

    private async Task<BrokerResponse> HandleLaunchAsync(BrokerLaunchPayload launch, CancellationToken cancellationToken)
    {
        SandboxPolicy? policy = null;
        if (!string.IsNullOrWhiteSpace(launch.PolicyJson))
        {
            policy = SandboxPolicyJson.Deserialize(launch.PolicyJson);
        }

        var sandboxRequest = new SandboxLaunchRequest
        {
            Kind = (SandboxKind)launch.SandboxKind,
            ExecutablePath = launch.Executable,
            Arguments = launch.Arguments,
            Environment = launch.Environment,
            WorkingDirectory = launch.WorkingDirectory,
            RedirectStandardInput = launch.RedirectStandardInput,
            RedirectStandardOutput = launch.RedirectStandardOutput,
            RedirectStandardError = launch.RedirectStandardError,
            PolicyOverride = policy,
        };

        if (launch.RedirectStandardInput || launch.RedirectStandardOutput || launch.RedirectStandardError)
        {
            return await LaunchRedirectedAsync(sandboxRequest, cancellationToken).ConfigureAwait(false);
        }

        var result = await _launcher.LaunchSandboxedAsync(sandboxRequest, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return new BrokerResponse { Ok = false, Error = result.Error };
        }

        return new BrokerResponse
        {
            Ok = true,
            ProcessId = result.Value!.ProcessId,
            Sandboxed = result.Value.Sandboxed,
            DegradedReason = result.Value.DegradedReason,
        };
    }

    private async Task<BrokerResponse> LaunchRedirectedAsync(
        SandboxLaunchRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
        var policy = request.PolicyOverride ?? BuildPolicy(request, settings);
        if (policy is null)
        {
            return new BrokerResponse { Ok = false, Error = "Could not build sandbox policy." };
        }

        var linuxLauncher = new LinuxSandboxLauncher(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LinuxSandboxLauncher>.Instance);

        var startInfo = BuildRedirectedStartInfo(linuxLauncher, policy, request);
        if (startInfo is null)
        {
            return new BrokerResponse { Ok = false, Error = "Could not build redirected start info." };
        }

        return StartManagedRedirectedProcess(linuxLauncher, startInfo);
    }

    private static ProcessStartInfo? BuildRedirectedStartInfo(
        LinuxSandboxLauncher linuxLauncher,
        SandboxPolicy policy,
        SandboxLaunchRequest request)
    {
        if (OperatingSystem.IsLinux() && linuxLauncher.IsHelperAvailable)
        {
            var built = linuxLauncher.BuildStartInfo(
                policy,
                request.ExecutablePath,
                request.Arguments.ToList(),
                request.Environment.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value,
                    StringComparer.Ordinal),
                request.WorkingDirectory,
                stdioPassthrough: true);
            if (!built.IsSuccess)
            {
                return null;
            }

            var linuxStartInfo = built.Value!;
            linuxStartInfo.RedirectStandardInput = true;
            linuxStartInfo.RedirectStandardOutput = true;
            linuxStartInfo.RedirectStandardError = true;
            return linuxStartInfo;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            UseShellExecute = false,
            WorkingDirectory = request.WorkingDirectory
                ?? Path.GetDirectoryName(request.ExecutablePath)
                ?? Environment.CurrentDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in request.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (var pair in request.Environment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        return startInfo;
    }

    private BrokerResponse StartManagedRedirectedProcess(
        LinuxSandboxLauncher linuxLauncher,
        ProcessStartInfo startInfo)
    {
        try
        {
            var process = global::System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                return new BrokerResponse { Ok = false, Error = "Process did not start." };
            }

            var managed = new BrokerManagedProcess(process);
            _processes[process.Id] = managed;
            return new BrokerResponse
            {
                Ok = true,
                ProcessId = process.Id,
                Sandboxed = OperatingSystem.IsLinux() && linuxLauncher.IsHelperAvailable,
            };
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return new BrokerResponse { Ok = false, Error = ex.Message };
        }
    }

    private BrokerResponse ReadProcessOutput(BrokerRequest request)
    {
        if (request.ProcessId is null || !_processes.TryGetValue(request.ProcessId.Value, out var managed))
        {
            return new BrokerResponse { Ok = false, Error = "Process not found." };
        }

        var chunk = managed.ReadOutput();
        return new BrokerResponse
        {
            Ok = true,
            OutputBase64 = chunk.Length == 0 ? null : Convert.ToBase64String(chunk),
        };
    }

    private BrokerResponse WriteProcessInput(BrokerRequest request)
    {
        if (request.ProcessId is null || string.IsNullOrEmpty(request.InputBase64))
        {
            return new BrokerResponse { Ok = false, Error = "Invalid write request." };
        }

        if (!_processes.TryGetValue(request.ProcessId.Value, out var managed))
        {
            return new BrokerResponse { Ok = false, Error = "Process not found." };
        }

        managed.WriteInput(Convert.FromBase64String(request.InputBase64));
        return new BrokerResponse { Ok = true };
    }

    private BrokerResponse KillProcess(BrokerRequest request)
    {
        if (request.ProcessId is null)
        {
            return new BrokerResponse { Ok = false, Error = "Missing process id." };
        }

        if (_processes.TryRemove(request.ProcessId.Value, out var managed))
        {
            managed.Dispose();
        }

        return new BrokerResponse { Ok = true };
    }

    private SandboxPolicy? BuildPolicy(SandboxLaunchRequest request, LauncherSettings settings)
    {
        var platform = _platform.GetPlatformInfo();
        var relicPaths = _pathProvider.GetPaths();
        var installPrefix = AppContext.BaseDirectory;

        return request.Kind switch
        {
            SandboxKind.Launcher => SandboxPolicyBuilder.BuildLauncher(
                relicPaths, settings, platform, installPrefix),
            SandboxKind.GameClient => SandboxPolicyBuilder.BuildGameClient(
                settings.InstallsRoot ?? platform.DefaultInstallsRoot,
                settings.SelectedVersion ?? string.Empty,
                settings.DataPath ?? platform.DefaultDataPath,
                request.Environment.TryGetValue("DOTNET_ROOT", out var root) ? root : null,
                installPrefix),
            SandboxKind.DedicatedServer => SandboxPolicyBuilder.BuildDedicatedServer(
                settings.InstallsRoot ?? platform.DefaultInstallsRoot,
                settings.SelectedServerVersion ?? string.Empty,
                settings.ServerDataPath ?? platform.DefaultServerDataPath,
                request.Environment.TryGetValue("DOTNET_ROOT", out var sroot) ? sroot : null,
                installPrefix,
                (ushort)(VintageStoryServerConfigReader.TryReadPort(settings.ServerDataPath ?? string.Empty)
                    ?? VintageStoryServerConfigReader.DefaultPort)),
            _ => null,
        };
    }

    private async Task<LauncherSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        var loaded = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return loaded.IsSuccess ? loaded.Value! : new LauncherSettings();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _cts.Dispose();
        }

        foreach (var pair in _processes)
        {
            pair.Value.Dispose();
        }

        _processes.Clear();
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }
}
