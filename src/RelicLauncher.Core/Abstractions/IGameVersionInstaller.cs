using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IGameVersionInstaller
{
    Task<Result<InstalledGameVersion>> InstallAsync(VersionInstallRequest request, CancellationToken cancellationToken = default);

    Task<Result> UninstallAsync(string installsRoot, string version, CancellationToken cancellationToken = default);

    GameVersionPackage? SelectClientPackage(GameVersionInfo version, PlatformInfo platform);
}
