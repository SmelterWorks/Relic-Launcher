using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IGameVersionCatalog
{
    bool LastCatalogWasStale { get; }

    Task<Result<IReadOnlyList<GameVersionInfo>>> GetVersionsAsync(
        GameVersionChannel? channel = null,
        CancellationToken cancellationToken = default);

    Task<Result<string?>> GetLatestStableVersionAsync(CancellationToken cancellationToken = default);
}
