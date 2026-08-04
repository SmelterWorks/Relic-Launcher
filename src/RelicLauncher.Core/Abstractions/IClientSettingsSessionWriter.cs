using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IClientSettingsSessionWriter
{
    Task<Result> ApplySessionAsync(string dataPath, CancellationToken cancellationToken = default);
}
