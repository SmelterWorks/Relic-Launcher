using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IModBlocklistService
{
    Task<Result<IReadOnlyList<ModBlocklistEntry>>> GetEntriesAsync(CancellationToken cancellationToken = default);

    Task<Result<ModBlocklistEntry?>> FindMatchAsync(
        string? modId,
        string? modVersion,
        CancellationToken cancellationToken = default);
}
