using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.SelfCheck;

internal sealed class SelfCheckAppPathProvider : IAppPathProvider
{
    public SelfCheckAppPathProvider(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public string RootDirectory { get; }

    public AppPaths GetPaths()
        => new()
        {
            RootDirectory = RootDirectory,
            SettingsFile = Path.Combine(RootDirectory, "settings.json"),
            LogsDirectory = Path.Combine(RootDirectory, "logs"),
            ThemesDirectory = Path.Combine(RootDirectory, "themes"),
            CacheDirectory = Path.Combine(RootDirectory, "cache"),
            SecretsDirectory = Path.Combine(RootDirectory, "secrets"),
        };
}
