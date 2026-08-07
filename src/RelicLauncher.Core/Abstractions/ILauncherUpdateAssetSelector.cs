using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Abstractions;

public interface ILauncherUpdateAssetSelector
{
    LauncherUpdateAsset? Select(LauncherUpdateInfo update, DetectedLauncherInstall install);
}
