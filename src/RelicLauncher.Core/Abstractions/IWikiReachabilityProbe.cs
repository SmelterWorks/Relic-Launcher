using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IWikiReachabilityProbe
{
    Task<Result<WikiReachabilityResult>> ProbeAsync(CancellationToken cancellationToken = default);
}
