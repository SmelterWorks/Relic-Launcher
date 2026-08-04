using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.Paths;

public sealed class AppPathProvider : RelicLauncher.Core.Abstractions.IAppPathProvider
{
    private readonly Lazy<AppPaths> _paths = new(CreatePaths);

    public AppPaths GetPaths() => _paths.Value;

    private static AppPaths CreatePaths()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RelicLauncher");

        return new AppPaths
        {
            RootDirectory = root,
            SettingsFile = Path.Combine(root, "settings.json"),
            LogsDirectory = Path.Combine(root, "logs"),
            ThemesDirectory = Path.Combine(root, "themes"),
            CacheDirectory = Path.Combine(root, "cache"),
            SecretsDirectory = Path.Combine(root, "secrets"),
        };
    }
}
