namespace RelicLauncher.Core.Models;

public sealed class AccountSessionStatus
{
    public bool IsSignedIn { get; init; }

    public bool RequiresTotp { get; init; }

    public string? PreLoginToken { get; init; }

    public string? Email { get; init; }

    public string? PlayerName { get; init; }

    public string? PlayerUid { get; init; }

    public string? SessionKey { get; init; }

    public string? SessionSignature { get; init; }

    public string? Entitlements { get; init; }

    public string? MpToken { get; init; }

    public string? HostGameServer { get; init; }
}
