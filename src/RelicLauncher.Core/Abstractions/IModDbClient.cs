using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IModDbClient
{
    Task<Result<ModSearchResult>> SearchAsync(ModSearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<ModDetails>> GetModAsync(string modIdOrAlias, CancellationToken cancellationToken = default);

    Task PrefetchCatalogAsync(CancellationToken cancellationToken = default);
}
