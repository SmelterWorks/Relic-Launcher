using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IAccountAuthService
{
    Task<Result<AccountSessionStatus>> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<Result<AccountSessionStatus>> LoginAsync(AccountCredentials credentials, CancellationToken cancellationToken = default);

    Task<Result> LogoutAsync(CancellationToken cancellationToken = default);

    Task<Result> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default);

    Task<Result> ValidateSessionAsync(CancellationToken cancellationToken = default);
}
