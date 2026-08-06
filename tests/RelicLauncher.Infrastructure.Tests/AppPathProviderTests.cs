using FluentAssertions;
using RelicLauncher.Infrastructure.Paths;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class AppPathProviderTests
{
    [Fact]
    public void GetPaths_UsesRelicLauncherUnderApplicationData()
    {
        var provider = new AppPathProvider();
        var paths = provider.GetPaths();

        paths.RootDirectory.Should().EndWith("RelicLauncher");
        paths.SettingsFile.Should().Be(Path.Combine(paths.RootDirectory, "settings.json"));
        paths.LogsDirectory.Should().Be(Path.Combine(paths.RootDirectory, "logs"));
        paths.ThemesDirectory.Should().Be(Path.Combine(paths.RootDirectory, "themes"));
        paths.CacheDirectory.Should().Be(Path.Combine(paths.RootDirectory, "cache"));
        paths.SecretsDirectory.Should().Be(Path.Combine(paths.RootDirectory, "secrets"));
    }
}
