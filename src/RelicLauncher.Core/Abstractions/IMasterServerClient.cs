using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IMasterServerClient
{
    Task<Result<MasterServerFetchResult>> FetchCatalogAsync(
        bool preferCache = true,
        CancellationToken cancellationToken = default);
}
