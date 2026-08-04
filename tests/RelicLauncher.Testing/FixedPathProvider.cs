using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Testing;

public sealed class FixedPathProvider(AppPaths paths) : IAppPathProvider
{
    public AppPaths GetPaths() => paths;
}
