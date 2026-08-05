using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IModDependencyInstallPlanner
{
    Task<Result<ModDependencyInstallPlan>> PlanAsync(
        ModReleaseInfo rootRelease,
        string gameVersion,
        IReadOnlyList<LocalModInfo> installed,
        CancellationToken cancellationToken = default);
}
