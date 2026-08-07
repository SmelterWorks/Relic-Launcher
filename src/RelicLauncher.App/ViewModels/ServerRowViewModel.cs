using CommunityToolkit.Mvvm.ComponentModel;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Security;

namespace RelicLauncher.App.ViewModels;

public sealed partial class ServerRowViewModel : ObservableObject
{
    public ServerRowViewModel(PublicServerSummary server, bool isFavorite)
    {
        Source = server;
        DisplayName = ServerDisplaySanitizer.SanitizeName(server.ServerName);
        DisplayDescription = ServerDisplaySanitizer.SanitizeDescription(server.Description);
        PlayersLabel = $"{server.Players}/{server.MaxPlayers}";
        VersionLabel = server.GameVersion ?? "unknown";
        Address = server.ServerAddress;
        IsFavorite = isFavorite;
        HasPlayers = server.Players > 0;
        ShowPasswordIcon = server.HasPassword;
        ShowWhitelistIcon = server.Whitelisted;
        ShowOfficialBadge = server.IsOfficialTopS;
        ModCountLabel = server.ModCount == 0 ? "Vanilla" : $"{server.ModCount} mods";
    }

    public PublicServerSummary Source { get; }

    public string DisplayName { get; }
    public string DisplayDescription { get; }
    public string PlayersLabel { get; }
    public string VersionLabel { get; }
    public string Address { get; }
    public string ModCountLabel { get; }
    public bool IsFavorite { get; }
    public bool HasPlayers { get; }
    public bool ShowPasswordIcon { get; }
    public bool ShowWhitelistIcon { get; }
    public bool ShowOfficialBadge { get; }

    [ObservableProperty]
    private bool _isSelected;
}
