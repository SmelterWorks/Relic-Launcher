using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IRecentServersStore
{
    Task<Result<IReadOnlyList<string>>> ListAsync(CancellationToken cancellationToken = default);

    Task RecordAsync(string address, CancellationToken cancellationToken = default);
}
