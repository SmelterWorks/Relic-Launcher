namespace RelicLauncher.Core.Models;

public sealed class GameServerStartRequest
{
    public required string InstallsRoot { get; init; }
    public required string Version { get; init; }
    public required string ServerDataPath { get; init; }
    public IProgress<double>? Progress { get; init; }
}
