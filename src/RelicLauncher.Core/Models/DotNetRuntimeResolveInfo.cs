namespace RelicLauncher.Core.Models;

public sealed class DotNetRuntimeResolveInfo
{
    public required string DotNetRoot { get; init; }

    public required bool IsManagedByRelic { get; init; }

    public required int MajorVersion { get; init; }
}
