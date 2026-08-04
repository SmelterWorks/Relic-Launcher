namespace RelicLauncher.Core.Models;

public sealed class AccountSessionStatus
{
    public bool IsSignedIn { get; init; }
    public string? Email { get; init; }
}
