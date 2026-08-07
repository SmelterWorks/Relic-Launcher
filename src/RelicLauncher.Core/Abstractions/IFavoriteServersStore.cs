using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IFavoriteServersStore
{
    Task<Result<IReadOnlyList<FavoriteServerEntry>>> ListAsync(CancellationToken cancellationToken = default);

    Task<Result> AddAsync(FavoriteServerEntry entry, CancellationToken cancellationToken = default);

    Task<Result> RemoveAsync(string address, CancellationToken cancellationToken = default);
}
