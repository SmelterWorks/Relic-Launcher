using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Abstractions;

public interface IRuntimePlatform
{
    PlatformInfo GetPlatformInfo();
}
