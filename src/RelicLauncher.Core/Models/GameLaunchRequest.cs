namespace RelicLauncher.Core.Models;

public sealed class GameLaunchRequest
{
    public required string InstallsRoot { get; init; }
    public required string Version { get; init; }
    public string? DataPath { get; init; }
}
