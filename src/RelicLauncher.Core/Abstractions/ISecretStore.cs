using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface ISecretStore
{
    Task<Result> SetAsync(string key, string value, CancellationToken cancellationToken = default);

    Task<Result<string?>> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(string key, CancellationToken cancellationToken = default);
}
