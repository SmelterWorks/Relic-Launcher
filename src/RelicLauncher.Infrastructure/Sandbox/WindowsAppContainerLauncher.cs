using System.ComponentModel;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

public sealed class WindowsAppContainerLauncher
{
    private readonly WindowsAppContainerAclGranter _aclGranter;
    private readonly ILogger<WindowsAppContainerLauncher> _logger;

    public WindowsAppContainerLauncher(
        WindowsAppContainerAclGranter aclGranter,
        ILogger<WindowsAppContainerLauncher> logger)
    {
        _aclGranter = aclGranter;
        _logger = logger;
    }

    public async Task<Result<SandboxLaunchResult>> LaunchAsync(
        string moniker,
        SandboxPolicy policy,
        SandboxLaunchRequest request,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Result<SandboxLaunchResult>.Failure("AppContainer is only available on Windows.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var profile = await WindowsAppContainerProfile.CreateOrDeriveAsync(moniker).ConfigureAwait(false);
        if (!profile.IsSuccess)
        {
            return Result<SandboxLaunchResult>.Failure(profile.Error!);
        }

        var grant = await _aclGranter.GrantPolicyPathsAsync(
            profile.Value!.Sid,
            policy.PathGrants.ToList(),
            cancellationToken).ConfigureAwait(false);
        if (!grant.IsSuccess)
        {
            return Result<SandboxLaunchResult>.Failure(grant.Error!);
        }

        var capabilities = WindowsAppContainerCapabilities.BuildForKind(policy.Kind);
        var launch = WindowsAppContainerProcess.Start(
            profile.Value!,
            capabilities,
            request);

        if (!launch.IsSuccess)
        {
            return Result<SandboxLaunchResult>.Failure(launch.Error!);
        }

        return Result<SandboxLaunchResult>.Success(new SandboxLaunchResult
        {
            ProcessId = launch.Value!.Id,
            Sandboxed = true,
        });
    }
}
