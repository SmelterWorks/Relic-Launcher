using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IModReleaseResolver
{
    Task<Result<ModReleaseInfo>> ResolveAsync(
        string modIdentifier,
        string gameVersion,
        CancellationToken cancellationToken = default);
}
