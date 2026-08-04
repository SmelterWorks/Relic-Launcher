using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.App.Services;

public interface IAccountBrowserLoginService
{
    Task<Result<AccountSessionStatus>> SignInAsync(string? emailHint, CancellationToken cancellationToken = default);
}
