namespace RelicLauncher.Core.Models;

public sealed class GameLaunchRequest
{
    public required string InstallsRoot { get; init; }
    public required string Version { get; init; }
    public string? DataPath { get; init; }
    public string? ConnectAddress { get; init; }
    public string? ConnectPassword { get; init; }
    public IProgress<double>? Progress { get; init; }
}
