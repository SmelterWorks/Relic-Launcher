namespace RelicLauncher.Core.Models;

public sealed class InstalledServerVersion
{
    public required string Version { get; init; }
    public required string InstallPath { get; init; }
    public string? ExecutablePath { get; init; }
    public bool ExecutableFound { get; init; }
    public DateTimeOffset InstalledAt { get; init; }
}
