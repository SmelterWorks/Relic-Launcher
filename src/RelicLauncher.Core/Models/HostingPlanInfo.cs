namespace RelicLauncher.Core.Models;

public sealed class HostingPlanInfo
{
    public required string Name { get; init; }
    public string? Subtitle { get; init; }
    public string? MonthlyPrice { get; init; }
    public string? AnnualPrice { get; init; }
    public IReadOnlyList<string> Highlights { get; init; } = [];
}
