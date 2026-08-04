using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IInstalledVersionStore
{
    Task<Result<IReadOnlyList<InstalledGameVersion>>> ListAsync(string installsRoot, CancellationToken cancellationToken = default);

    Task<Result> SaveAsync(string installsRoot, IReadOnlyList<InstalledGameVersion> versions, CancellationToken cancellationToken = default);
}
