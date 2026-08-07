using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;
using RelicLauncher.Core.Server;

namespace RelicLauncher.Infrastructure.Sandbox;

public sealed class PassthroughSandboxBrokerClient : ISandboxBrokerClient
{
    private readonly LinuxSandboxLauncher _linuxLauncher;
    private readonly WindowsSandboxLauncher _windowsLauncher;
    private readonly ILauncherSettingsStore _settingsStore;
    private readonly IRuntimePlatform _platform;
    private readonly IAppPathProvider _pathProvider;
    private readonly ILogger<PassthroughSandboxBrokerClient> _logger;

    public PassthroughSandboxBrokerClient(
        LinuxSandboxLauncher linuxLauncher,
        WindowsSandboxLauncher windowsLauncher,
        ILauncherSettingsStore settingsStore,
        IRuntimePlatform platform,
        IAppPathProvider pathProvider,
        ILogger<PassthroughSandboxBrokerClient> logger)
    {
        _linuxLauncher = linuxLauncher;
        _windowsLauncher = windowsLauncher;
        _settingsStore = settingsStore;
        _platform = platform;
        _pathProvider = pathProvider;
        _logger = logger;
    }

    public async Task<Result<SandboxLaunchResult>> LaunchSandboxedAsync(
        SandboxLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.ProcessIsolationEnabled)
        {
            return LaunchRawAsync(request);
        }

        var policy = request.PolicyOverride ?? await BuildPolicyAsync(request, settings, cancellationToken)
            .ConfigureAwait(false);
        if (policy is null)
        {
            return LaunchRawAsync(request);
        }

        if (OperatingSystem.IsLinux() && _linuxLauncher.IsHelperAvailable)
        {
            var stdio = request.RedirectStandardInput
                || request.RedirectStandardOutput
                || request.RedirectStandardError;
            var start = _linuxLauncher.BuildStartInfo(
                policy,
                request.ExecutablePath,
                request.Arguments,
                request.Environment,
                request.WorkingDirectory,
                stdio);
            if (!start.IsSuccess)
            {
                return Result<SandboxLaunchResult>.Failure(start.Error!);
            }

            return StartFromInfo(start.Value!, sandboxed: true, null);
        }

        if (OperatingSystem.IsWindows())
        {
            var win = await _windowsLauncher.LaunchAsync(
                policy,
                request,
                cancellationToken).ConfigureAwait(false);
            return win;
        }

        return LaunchRawAsync(request);
    }

    public Task<Result> OpenDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(OpenFolderRaw(directoryPath));

    public Task<Result> OpenUrlAsync(string url, CancellationToken cancellationToken = default) =>
        Task.FromResult(OpenUrlRaw(url));

    public Task<Result> RunInstallerAsync(
        string installerPath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = false,
            };
            foreach (var arg in arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var process = global::System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                return Task.FromResult(Result.Failure("Installer did not start."));
            }

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return Task.FromResult(Result.Failure(ex.Message));
        }
    }

    public async Task<Result> WriteFileAsync(
        string destinationPath,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(destinationPath, content, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure(ex.Message);
        }
    }

    public Task<byte[]> ReadProcessOutputAsync(int processId, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<byte>());

    public Task<Result> WriteProcessInputAsync(
        int processId,
        string text,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());

    public Task<Result> KillProcessAsync(int processId, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());

    private async Task<SandboxPolicy?> BuildPolicyAsync(
        SandboxLaunchRequest request,
        LauncherSettings settings,
        CancellationToken cancellationToken)
    {
        var platform = _platform.GetPlatformInfo();
        var relicPaths = _pathProvider.GetPaths();
        var installPrefix = AppContext.BaseDirectory;

        switch (request.Kind)
        {
            case SandboxKind.Launcher:
                return SandboxPolicyBuilder.BuildLauncher(relicPaths, settings, platform, installPrefix);
            case SandboxKind.GameClient:
                var version = settings.SelectedVersion ?? string.Empty;
                var dataPath = settings.DataPath ?? platform.DefaultDataPath;
                var dotNetRoot = request.Environment.TryGetValue("DOTNET_ROOT", out var root) ? root : null;
                return SandboxPolicyBuilder.BuildGameClient(
                    settings.InstallsRoot ?? platform.DefaultInstallsRoot,
                    version,
                    dataPath,
                    dotNetRoot,
                    installPrefix);
            case SandboxKind.DedicatedServer:
                var serverVersion = settings.SelectedServerVersion ?? string.Empty;
                var serverData = settings.ServerDataPath ?? platform.DefaultServerDataPath;
                var serverDotNet = request.Environment.TryGetValue("DOTNET_ROOT", out var sroot) ? sroot : null;
                var port = VintageStoryServerConfigReader.TryReadPort(serverData.Trim())
                    ?? VintageStoryServerConfigReader.DefaultPort;

                return SandboxPolicyBuilder.BuildDedicatedServer(
                    settings.InstallsRoot ?? platform.DefaultInstallsRoot,
                    serverVersion,
                    serverData,
                    serverDotNet,
                    installPrefix,
                    (ushort)port);
            default:
                return null;
        }
    }

    private Result<SandboxLaunchResult> LaunchRawAsync(SandboxLaunchRequest request)
    {
        try
        {
            var startInfo = new ProcessStartInfo
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

            return StartFromInfo(startInfo, sandboxed: false, "Isolation disabled or unavailable.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return Result<SandboxLaunchResult>.Failure(ex.Message);
        }
    }

    private Result<SandboxLaunchResult> StartFromInfo(
        ProcessStartInfo startInfo,
        bool sandboxed,
        string? degradedReason)
    {
        try
        {
            var process = global::System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                return Result<SandboxLaunchResult>.Failure("Process did not start.");
            }

            _logger.LogInformation(
                "Started {Exe} pid {Pid} sandboxed={Sandboxed}",
                startInfo.FileName,
                process.Id,
                sandboxed);

            return Result<SandboxLaunchResult>.Success(new SandboxLaunchResult
            {
                ProcessId = process.Id,
                Sandboxed = sandboxed,
                DegradedReason = degradedReason,
            });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return Result<SandboxLaunchResult>.Failure(ex.Message);
        }
    }

    private static Result OpenFolderRaw(string folderPath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo("explorer.exe", folderPath) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo("open", folderPath) { UseShellExecute = true });
            }
            else
            {
                global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo("xdg-open", folderPath) { UseShellExecute = true });
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static Result OpenUrlRaw(string url)
    {
        try
        {
            global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            return Result.Success();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return Result.Failure(ex.Message);
        }
    }

    private async Task<LauncherSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        var loaded = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return loaded.IsSuccess ? loaded.Value! : new LauncherSettings();
    }
}
