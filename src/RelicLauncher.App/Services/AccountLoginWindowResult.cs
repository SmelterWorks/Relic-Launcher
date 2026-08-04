using System.Net;

namespace RelicLauncher.App.Services;

public sealed class AccountLoginWindowResult
{
    public bool Canceled { get; init; }
    public string? Email { get; init; }
    public IReadOnlyList<Cookie> Cookies { get; init; } = [];
}
