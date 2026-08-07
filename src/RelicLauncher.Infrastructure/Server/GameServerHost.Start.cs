using System.Text;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Versions;

namespace RelicLauncher.Infrastructure.Server;

public sealed partial class GameServerHost
{
    private async Task<Result> StartCoreAsync(GameServerStartRequest request, CancellationToken cancellationToken)
    {
        if (State is ServerProcessState.Running or ServerProcessState.Starting)
        {
            return Result.Failure("Server is already running.");
        }

        var validation = ValidateStartRequest(request);
        if (!validation.IsSuccess)
        {
            return Result.Failure(validation.Error!);
        }

        var (exe, installPath) = validation.Value!;
        var runtimeMajor = GameDotNetRuntimeRequirements.TryGetRequiredMajor(request.Version);
        if (!runtimeMajor.IsSuccess)
        {
            return Result.Failure(runtimeMajor.Error ?? "Unsupported game version for .NET runtime.");
        }

        SetState(ServerProcessState.Starting);

        var runtime = await _runtimeProvisioner.EnsureAsync(
            runtimeMajor.Value,
            request.Progress,
            cancellationToken).ConfigureAwait(false);
        if (!runtime.IsSuccess)
        {
            SetState(ServerProcessState.Stopped);
            return Result.Failure(runtime.Error ?? "Could not provision the required .NET runtime.");
        }

        var launch = LaunchProcess(request, exe, installPath, runtime.Value!);
        if (!launch.IsSuccess)
        {
            SetState(ServerProcessState.Stopped);
            return Result.Failure(launch.Error!);
        }

        AttachProcess(launch.Value!, request);
        return Result.Success();
    }

    private static Result<(string Exe, string InstallPath)> ValidateStartRequest(GameServerStartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InstallsRoot))
        {
            return Result<(string, string)>.Failure("Installs root is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Result<(string, string)>.Failure("No server version is selected.");
        }

        var installPath = GameServerInstallLayout.GetServerDirectory(request.InstallsRoot, request.Version);
        if (!Directory.Exists(installPath))
        {
            return Result<(string, string)>.Failure($"Server version {request.Version} is not installed.");
        }

        var exe = VintageStoryServerExecutableLocator.FindServerExecutable(installPath);
        if (exe is null)
        {
            return Result<(string, string)>.Failure("No server executable found for the selected version.");
        }

        return Result<(string, string)>.Success((exe, installPath));
    }

    private Result<global::System.Diagnostics.Process> LaunchProcess(
        GameServerStartRequest request,
        string exe,
        string installPath,
        DotNetRuntimeResolveInfo runtime)
    {
        var dataPath = request.ServerDataPath.Trim();
        Directory.CreateDirectory(dataPath);

        var startInfo = new global::System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? installPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--dataPath");
        startInfo.ArgumentList.Add(dataPath);

        if (runtime.IsManagedByRelic)
        {
            startInfo.Environment["DOTNET_ROOT"] = runtime.DotNetRoot;
        }

        try
        {
            var process = global::System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Process.Start returned null.");
            return Result<global::System.Diagnostics.Process>.Success(process);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                        or UnauthorizedAccessException
                                        or InvalidOperationException
                                        or IOException)
        {
            _logger.LogError(ex, "Failed to start server {Version}", request.Version);
            return Result<global::System.Diagnostics.Process>.Failure(ex.Message);
        }
    }

    private void AttachProcess(global::System.Diagnostics.Process process, GameServerStartRequest request)
    {
        _process = process;
        _lastRequest = request;
        RunningVersion = request.Version;
        _readCts = new CancellationTokenSource();
        _stdoutTask = ReadStreamAsync(process.StandardOutput, _readCts.Token);
        _stderrTask = ReadStreamAsync(process.StandardError, _readCts.Token);
        _exitTask = MonitorExitAsync(process);
        AppendLine($"Started server {request.Version} (pid {process.Id}).");
        SetState(ServerProcessState.Running);
        _logger.LogInformation("Started Vintage Story server {Version} pid {Pid}", request.Version, process.Id);
    }
}
