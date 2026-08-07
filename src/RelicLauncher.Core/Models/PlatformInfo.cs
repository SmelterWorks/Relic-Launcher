namespace RelicLauncher.Core.Models;

public sealed class PlatformInfo
{
    public required HostOs Os { get; init; }
    public required HostArch Arch { get; init; }
    public required string ClientPackageKey { get; init; }
    public required string ServerPackageKey { get; init; }
    public required string DefaultDataPath { get; init; }
    public required string DefaultServerDataPath { get; init; }
    public required string DefaultInstallsRoot { get; init; }
}
