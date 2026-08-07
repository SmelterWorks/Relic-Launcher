namespace RelicLauncher.Core.SelfCheck;

public sealed class SelfCheckReport
{
    public SelfCheckReport(IReadOnlyList<SelfCheckItem> items)
    {
        Items = items;
    }

    public IReadOnlyList<SelfCheckItem> Items { get; }

    public bool Passed => Items.All(static item =>
        item.Status is SelfCheckStatus.Pass or SelfCheckStatus.Skip);

    public int PassCount => Items.Count(static item => item.Status == SelfCheckStatus.Pass);

    public int FailCount => Items.Count(static item => item.Status == SelfCheckStatus.Fail);

    public int SkipCount => Items.Count(static item => item.Status == SelfCheckStatus.Skip);
}
