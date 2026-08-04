namespace RelicLauncher.Core.Models;

public sealed class AccountCredentials
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public string? TotpCode { get; init; }
    public string? PreLoginToken { get; init; }
}
