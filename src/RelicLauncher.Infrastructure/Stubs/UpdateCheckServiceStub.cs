using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Stubs;

public sealed class UpdateCheckServiceStub : IUpdateCheckService
{
    public Task<Result<LauncherUpdateCheckResult>> CheckForLauncherUpdateAsync(
        LauncherUpdateCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Result<LauncherUpdateCheckResult>.Success(new LauncherUpdateCheckResult()));
    }
}
