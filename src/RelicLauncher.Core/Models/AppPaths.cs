namespace RelicLauncher.Core.Models;

public sealed class AppPaths
{
    public required string RootDirectory { get; init; }
    public required string SettingsFile { get; init; }
    public required string LogsDirectory { get; init; }
    public required string ThemesDirectory { get; init; }
    public required string CacheDirectory { get; init; }
    public required string SecretsDirectory { get; init; }
}
