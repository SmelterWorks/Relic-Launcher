namespace RelicLauncher.Core.Models;

public sealed class AppPaths
{
    public required string RootDirectory { get; init; }
    public required string SettingsFile { get; init; }
    public required string LogsDirectory { get; init; }
    public required string ThemesDirectory { get; init; }
}
