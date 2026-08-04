using RelicLauncher.Core.Models;

namespace RelicLauncher.Testing;

public sealed class TempAppPaths : IDisposable
{
    public AppPaths Paths { get; }

    public TempAppPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "RelicLauncherTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Paths = new AppPaths
        {
            RootDirectory = root,
            SettingsFile = Path.Combine(root, "settings.json"),
            LogsDirectory = Path.Combine(root, "logs"),
            ThemesDirectory = Path.Combine(root, "themes"),
            CacheDirectory = Path.Combine(root, "cache"),
            SecretsDirectory = Path.Combine(root, "secrets"),
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(Paths.RootDirectory))
        {
            Directory.Delete(Paths.RootDirectory, recursive: true);
        }
    }
}
