using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Stubs;

public sealed class GameLocatorStub : IGameLocator
{
    public Task<Result<GameInstallInfo>> LocateAsync(string? configuredPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Task.FromResult(Result<GameInstallInfo>.Failure("No Vintage Story install path is configured."));
        }

        if (!PathValidator.TryGetFullPath(configuredPath, out var fullPath, out var pathError))
        {
            return Task.FromResult(Result<GameInstallInfo>.Failure(pathError));
        }

        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult(Result<GameInstallInfo>.Failure($"Install directory does not exist: {fullPath}"));
        }

        var executable = VintageStoryExecutableLocator.FindClientExecutable(fullPath);
        var info = new GameInstallInfo
        {
            InstallPath = fullPath,
            ExecutableFound = executable is not null,
            ExecutablePath = executable,
            DetectedVersion = null,
        };

        return Task.FromResult(Result<GameInstallInfo>.Success(info));
    }
}
