using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Platform;

namespace RelicLauncher.Infrastructure.Tests;

internal sealed class FakeRuntimePlatform : IRuntimePlatform
{
    public PlatformInfo Info { get; set; } = new RuntimePlatform().GetPlatformInfo();

    public PlatformInfo GetPlatformInfo() => Info;
}
