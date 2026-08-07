using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface ILanServerScanner
{
    Task<Result<IReadOnlyList<LanServerSummary>>> ScanAsync(CancellationToken cancellationToken = default);
}
