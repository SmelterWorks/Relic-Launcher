using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IProcessRunner
{
    Task<Result> StartAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);

    Task<Result> StartAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken = default);
}
