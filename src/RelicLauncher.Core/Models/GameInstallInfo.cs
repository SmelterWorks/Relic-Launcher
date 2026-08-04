namespace RelicLauncher.Core.Models;

public sealed class GameInstallInfo
{
    public required string InstallPath { get; init; }
    public string? DetectedVersion { get; init; }
    public bool ExecutableFound { get; init; }
    public string? ExecutablePath { get; init; }
}
