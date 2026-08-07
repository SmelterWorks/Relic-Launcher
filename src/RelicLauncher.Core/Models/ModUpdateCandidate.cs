namespace RelicLauncher.Core.Models;

public sealed class ModUpdateCandidate
{
    public required string ModId { get; init; }
    public required string Name { get; init; }
    public required string InstalledVersion { get; init; }
    public required string AvailableVersion { get; init; }
    public required ModReleaseInfo Release { get; init; }
    public int InstalledFileId { get; init; }
}
