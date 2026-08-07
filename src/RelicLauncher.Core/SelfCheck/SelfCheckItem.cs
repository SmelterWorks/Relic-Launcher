namespace RelicLauncher.Core.SelfCheck;

public sealed class SelfCheckItem
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required SelfCheckStatus Status { get; init; }

    public string? Detail { get; init; }

    public static SelfCheckItem Pass(string id, string name, string? detail = null)
        => new() { Id = id, Name = name, Status = SelfCheckStatus.Pass, Detail = detail };

    public static SelfCheckItem Fail(string id, string name, string? detail = null)
        => new() { Id = id, Name = name, Status = SelfCheckStatus.Fail, Detail = detail };

    public static SelfCheckItem Skip(string id, string name, string? detail = null)
        => new() { Id = id, Name = name, Status = SelfCheckStatus.Skip, Detail = detail };
}
