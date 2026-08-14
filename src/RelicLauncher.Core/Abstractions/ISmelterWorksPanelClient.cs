using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface ISmelterWorksPanelClient
{
    Task<Result<IReadOnlyList<PanelServerSummary>>> GetMyServersAsync(string apiToken, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<MigrationJobSummary>>> GetMigrationsAsync(string apiToken, CancellationToken cancellationToken = default);
}
