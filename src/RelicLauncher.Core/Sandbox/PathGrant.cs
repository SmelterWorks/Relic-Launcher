namespace RelicLauncher.Core.Sandbox;

public sealed class PathGrant
{
    public required string Path { get; init; }

    public PathAccess Access { get; init; }
}
