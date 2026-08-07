namespace RelicLauncher.Core.Models;

public sealed class ModUpdateCheckResult
{
    public IReadOnlyList<ModUpdateCandidate> Candidates { get; init; } = [];
    public int SkippedOptOutCount { get; init; }
    public int SkippedLocalOnlyCount { get; init; }
    public int SkippedUnresolvedCount { get; init; }
    public bool WasThrottled { get; init; }

    public ModUpdateCheckResult WithThrottled(bool wasThrottled)
        => new()
        {
            Candidates = Candidates,
            SkippedOptOutCount = SkippedOptOutCount,
            SkippedLocalOnlyCount = SkippedLocalOnlyCount,
            SkippedUnresolvedCount = SkippedUnresolvedCount,
            WasThrottled = wasThrottled,
        };
}
