using RelicLauncher.Core.Models;
using RelicLauncher.Core.Security;

namespace RelicLauncher.App.ViewModels;

public sealed class LanServerRowViewModel
{
    public LanServerRowViewModel(LanServerSummary server)
    {
        Source = server;
        DisplayName = ServerDisplaySanitizer.SanitizeName(server.ServerName ?? server.Address);
        DisplayDescription = ServerDisplaySanitizer.SanitizeDescription(server.Description);
        PlayersLabel = server.MaxPlayers > 0
            ? $"{server.Players}/{server.MaxPlayers}"
            : server.Players > 0
                ? $"{server.Players} players"
                : "Unknown players";
        VersionLabel = server.GameVersion ?? "unknown";
        Address = server.Address;
        ShowPasswordIcon = server.HasPassword;
        ShowLocalHostedBadge = server.IsLocalHosted;
        HasPlayers = server.Players > 0;
    }

    public LanServerSummary Source { get; }
    public string DisplayName { get; }
    public string DisplayDescription { get; }
    public string PlayersLabel { get; }
    public string VersionLabel { get; }
    public string Address { get; }
    public bool ShowPasswordIcon { get; }
    public bool ShowLocalHostedBadge { get; }
    public bool HasPlayers { get; }
}
