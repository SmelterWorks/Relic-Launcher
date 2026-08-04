using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Stubs;

public sealed class UpdateCheckServiceStub : IUpdateCheckService
{
    public Task<Result<string?>> CheckForLauncherUpdateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Result<string?>.Success(null));
    }
}
