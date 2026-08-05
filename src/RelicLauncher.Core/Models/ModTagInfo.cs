namespace RelicLauncher.Core.Models;

public sealed class ModTagInfo
{
    public required string TagId { get; init; }
    public required string Name { get; init; }
    public string? Color { get; init; }
}
