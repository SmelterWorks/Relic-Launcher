using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IProcessRunner
{
    Task<Result> StartAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
