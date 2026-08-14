namespace RelicLauncher.Core.Models;

public sealed class PanelServerSummary
{
    public string Uuid { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ConnectAddress { get; init; }
    public bool DaemonOnline { get; init; }
}
