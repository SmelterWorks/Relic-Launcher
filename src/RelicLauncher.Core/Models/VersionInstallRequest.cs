namespace RelicLauncher.Core.Models;

public sealed class VersionInstallRequest
{
    public required string InstallsRoot { get; init; }
    public required GameVersionInfo Version { get; init; }
    public IProgress<double>? Progress { get; init; }
}
