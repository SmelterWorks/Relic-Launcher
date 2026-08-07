namespace RelicLauncher.Core.Models;

public sealed class ServerInstallRequest
{
    public required string InstallsRoot { get; init; }
    public required GameVersionInfo Version { get; init; }
    public IProgress<double>? Progress { get; init; }
}
