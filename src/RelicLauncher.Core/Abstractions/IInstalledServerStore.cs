using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IInstalledServerStore
{
    Task<Result<IReadOnlyList<InstalledServerVersion>>> ListAsync(string installsRoot, CancellationToken cancellationToken = default);

    Task<Result> SaveAsync(string installsRoot, IReadOnlyList<InstalledServerVersion> versions, CancellationToken cancellationToken = default);
}
