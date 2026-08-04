namespace RelicLauncher.Core.Models;

public sealed class GameVersionInfo
{
    public required string Version { get; init; }
    public required GameVersionChannel Channel { get; init; }
    public IReadOnlyList<GameVersionPackage> Packages { get; init; } = [];
    public bool IsLatest { get; init; }
}
