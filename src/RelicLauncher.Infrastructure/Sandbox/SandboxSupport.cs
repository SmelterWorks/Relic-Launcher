using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

public sealed class SandboxSupport : ISandboxSupport
{
    private readonly ILauncherSettingsStore _settingsStore;
    private readonly LinuxSandboxLauncher _linuxLauncher;
    private readonly ILogger<SandboxSupport> _logger;
    private readonly int? _landlockAbi;
    private readonly bool _seccompAvailable;

    public SandboxSupport(
        ILauncherSettingsStore settingsStore,
        LinuxSandboxLauncher linuxLauncher,
        ILogger<SandboxSupport> logger)
    {
        _settingsStore = settingsStore;
        _linuxLauncher = linuxLauncher;
        _logger = logger;
        _landlockAbi = OperatingSystem.IsLinux() ? LinuxSandboxLauncher.ProbeLandlockAbi() : null;
        _seccompAvailable = OperatingSystem.IsLinux() && LinuxSandboxLauncher.ProbeSeccomp();
    }

    public bool IsIsolationAvailable =>
        OperatingSystem.IsWindows() || (_linuxLauncher.IsHelperAvailable && (_landlockAbi is not null || _seccompAvailable));

    public bool IsRunningSandboxed =>
        string.Equals(
            Environment.GetEnvironmentVariable(SandboxEnvironment.RunningSandboxed),
            "1",
            StringComparison.Ordinal);

    public bool IsBrokerConnected =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SandboxEnvironment.BrokerSocketPath))
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SandboxEnvironment.BrokerPipeName));

    public int? LandlockAbi => _landlockAbi;

    public bool SeccompAvailable => _seccompAvailable;

    public bool AppContainerAvailable => OperatingSystem.IsWindows();

    public string GetStatusSummary()
    {
        var status = GetStatus();
        if (!status.LauncherSandboxed && !status.SeccompActive && !status.AppContainerActive)
        {
            return "Disabled or unavailable";
        }

        var parts = new List<string>();
        if (status.LandlockAbi is not null)
        {
            parts.Add($"Landlock ABI {status.LandlockAbi}");
        }

        if (status.SeccompActive)
        {
            parts.Add("seccomp");
        }

        if (status.AppContainerActive)
        {
            parts.Add("AppContainer");
        }

        if (status.ClientDegradedReason is not null)
        {
            parts.Add("client degraded");
        }

        return string.Join(" + ", parts);
    }

    public SandboxIsolationStatus GetStatus()
    {
        return new SandboxIsolationStatus
        {
            LauncherSandboxed = IsRunningSandboxed,
            ClientLaunchSandboxed = OperatingSystem.IsLinux() && _linuxLauncher.IsHelperAvailable,
            ServerLaunchSandboxed = OperatingSystem.IsLinux() && _linuxLauncher.IsHelperAvailable,
            LandlockAbi = _landlockAbi,
            SeccompActive = _seccompAvailable && IsRunningSandboxed,
            AppContainerActive = OperatingSystem.IsWindows() && IsRunningSandboxed,
        };
    }
}
