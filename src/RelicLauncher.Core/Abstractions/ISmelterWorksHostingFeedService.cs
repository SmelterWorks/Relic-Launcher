using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface ISmelterWorksHostingFeedService
{
    Task<Result<IReadOnlyList<HostingPlanInfo>>> GetPlansAsync(CancellationToken cancellationToken = default);
}
