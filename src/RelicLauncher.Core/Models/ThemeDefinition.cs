namespace RelicLauncher.Core.Models;

public sealed class ThemeDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public bool IsBuiltIn { get; init; }
    public string? ResourceUri { get; init; }
}
