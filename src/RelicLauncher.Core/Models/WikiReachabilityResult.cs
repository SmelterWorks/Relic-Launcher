namespace RelicLauncher.Core.Models;

public sealed class WikiReachabilityResult
{
    public required WikiReachabilityStatus Status { get; init; }
    public string? Detail { get; init; }
    public int? HttpStatusCode { get; init; }

    public bool IsReachable => Status == WikiReachabilityStatus.Reachable;
}
