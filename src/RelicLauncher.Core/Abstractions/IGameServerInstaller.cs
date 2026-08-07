using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IGameServerInstaller
{
    Task<Result<InstalledServerVersion>> InstallAsync(ServerInstallRequest request, CancellationToken cancellationToken = default);

    Task<Result> UninstallAsync(string installsRoot, string version, CancellationToken cancellationToken = default);

    GameVersionPackage? SelectServerPackage(GameVersionInfo version, PlatformInfo platform);
}
