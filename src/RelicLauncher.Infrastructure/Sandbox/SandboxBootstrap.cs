using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

public static partial class SandboxBootstrap
{
    public const string BrokerArgument = "--sandbox-broker";

    public static bool ShouldRunBroker(string[] args)
    {
        if (OperatingSystem.IsMacOS())
        {
            return false;
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(SandboxEnvironment.SkipBootstrap),
                "1",
                StringComparison.Ordinal))
        {
            return false;
        }

        return args.Contains(BrokerArgument, StringComparer.Ordinal)
            || string.Equals(
                Environment.GetEnvironmentVariable(SandboxEnvironment.BrokerRole),
                SandboxEnvironment.BrokerRoleValue,
                StringComparison.Ordinal);
    }

    public static bool IsSandboxedUi()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(SandboxEnvironment.RunningSandboxed),
            "1",
            StringComparison.Ordinal);
    }

    public static async Task<int> RunBrokerAsync(
        string[] args,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var socketPath = Environment.GetEnvironmentVariable(SandboxEnvironment.BrokerSocketPath);
        if (string.IsNullOrWhiteSpace(socketPath))
        {
            socketPath = Path.Combine(Path.GetTempPath(), "relic-broker.sock");
        }

        var host = services.GetRequiredService<SandboxBrokerHost>();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var brokerTask = host.RunAsync(socketPath, linked.Token);
        var uiTask = LaunchSandboxedUiAsync(args, socketPath, services, linked.Token);

        var uiExit = await uiTask.ConfigureAwait(false);
        await linked.CancelAsync().ConfigureAwait(false);
        try
        {
            await brokerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when UI exits.
        }

        return uiExit;
    }

    public static async Task<int> TryBootstrapAsync(
        string[] args,
        ILauncherSettingsStore settingsStore,
        IAppPathProvider pathProvider,
        CancellationToken cancellationToken = default)
    {
        if (IsSandboxedUi() || ShouldRunBroker(args))
        {
            return -1;
        }

        var settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.IsSuccess || !settings.Value!.ProcessIsolationEnabled)
        {
            return -1;
        }

        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows())
        {
            return -1;
        }

        if (OperatingSystem.IsLinux() && string.IsNullOrEmpty(LinuxSandboxLauncher.ResolveHelperPathStatic()))
        {
            return -1;
        }

        var socketPath = Path.Combine(pathProvider.GetPaths().RootDirectory, "broker.sock");
        try
        {
            var socketDir = Path.GetDirectoryName(socketPath)!;
            Directory.CreateDirectory(socketDir);
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
        catch
        {
            socketPath = Path.Combine(Path.GetTempPath(), $"relic-broker-{Environment.ProcessId}.sock");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath ?? "dotnet",
            UseShellExecute = false,
        };

        if (string.Equals(startInfo.FileName, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "RelicLauncher.App.dll"));
        }

        startInfo.ArgumentList.Add(BrokerArgument);
        startInfo.Environment[SandboxEnvironment.BrokerRole] = SandboxEnvironment.BrokerRoleValue;
        startInfo.Environment[SandboxEnvironment.BrokerSocketPath] = socketPath;

        using var process = global::System.Diagnostics.Process.Start(startInfo);
        if (process is null)
        {
            return -1;
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }

    private static async Task<int> LaunchSandboxedUiAsync(
        string[] args,
        string socketPath,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var settingsStore = services.GetRequiredService<ILauncherSettingsStore>();
        var platform = services.GetRequiredService<IRuntimePlatform>();
        var pathProvider = services.GetRequiredService<IAppPathProvider>();
        var linuxLauncher = services.GetRequiredService<LinuxSandboxLauncher>();
        var windowsLauncher = services.GetRequiredService<WindowsSandboxLauncher>();

        var settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var launcherSettings = settings.IsSuccess ? settings.Value! : new LauncherSettings();
        var platformInfo = platform.GetPlatformInfo();
        var policy = SandboxPolicyBuilder.BuildLauncher(
            pathProvider.GetPaths(),
            launcherSettings,
            platformInfo,
            AppContext.BaseDirectory);

        var (uiExecutable, uiArgs) = BuildUiLaunchCommand(args);

        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [SandboxEnvironment.BrokerSocketPath] = socketPath,
            [SandboxEnvironment.RunningSandboxed] = "1",
        };

        if (OperatingSystem.IsLinux() && linuxLauncher.IsHelperAvailable)
        {
            return await LaunchLinuxUiAsync(
                linuxLauncher,
                policy,
                uiExecutable,
                uiArgs,
                env,
                cancellationToken).ConfigureAwait(false);
        }

        if (OperatingSystem.IsWindows())
        {
            return await LaunchWindowsUiAsync(
                windowsLauncher,
                policy,
                uiExecutable,
                uiArgs,
                env,
                cancellationToken).ConfigureAwait(false);
        }

        return -1;
    }

    private static async Task<int> LaunchLinuxUiAsync(
        LinuxSandboxLauncher linuxLauncher,
        SandboxPolicy policy,
        string uiExecutable,
        List<string> uiArgs,
        Dictionary<string, string?> env,
        CancellationToken cancellationToken)
    {
        var start = linuxLauncher.BuildStartInfo(
            policy,
            uiExecutable,
            uiArgs,
            env,
            AppContext.BaseDirectory,
            stdioPassthrough: false);
        if (!start.IsSuccess)
        {
            return 1;
        }

        using var process = global::System.Diagnostics.Process.Start(start.Value!);
        if (process is null)
        {
            return 1;
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }

    private static async Task<int> LaunchWindowsUiAsync(
        WindowsSandboxLauncher windowsLauncher,
        SandboxPolicy policy,
        string uiExecutable,
        List<string> uiArgs,
        Dictionary<string, string?> env,
        CancellationToken cancellationToken)
    {
        var launchRequest = new SandboxLaunchRequest
        {
            Kind = SandboxKind.Launcher,
            ExecutablePath = uiExecutable,
            Arguments = uiArgs,
            Environment = env,
            WorkingDirectory = AppContext.BaseDirectory,
        };

        var win = await windowsLauncher.LaunchAsync(
            policy,
            launchRequest,
            cancellationToken).ConfigureAwait(false);
        if (!win.IsSuccess)
        {
            return 1;
        }

        try
        {
            using var process = global::System.Diagnostics.Process.GetProcessById(win.Value!.ProcessId);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (ArgumentException)
        {
            return 1;
        }
    }
}
