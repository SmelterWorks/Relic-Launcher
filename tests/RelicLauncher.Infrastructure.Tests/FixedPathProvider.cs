using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.Tests;

internal sealed class FixedPathProvider(AppPaths paths) : IAppPathProvider
{
    public AppPaths GetPaths() => paths;
}
