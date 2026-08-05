using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IDotNetRuntimeProvisioner
{
    Task<Result<DotNetRuntimeResolveInfo>> EnsureAsync(
        int majorVersion,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
