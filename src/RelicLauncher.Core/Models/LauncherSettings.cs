using RelicLauncher.Core.Constants;

namespace RelicLauncher.Core.Models;

public sealed class LauncherSettings
{
    public const string DefaultThemeId = RelicDefaults.ThemeId;

    public string SelectedThemeId { get; set; } = DefaultThemeId;
    public string? GameInstallPath { get; set; }
    public string? InstallsRoot { get; set; }
    public string? SelectedVersion { get; set; }
    public string? DataPath { get; set; }
    public string? SelectedServerVersion { get; set; }
    public string? ServerDataPath { get; set; }
    public bool ConfirmBeforeExit { get; set; }
    public bool WarnOnBlockedMods { get; set; } = true;
    public ModUpdateMode ModUpdateMode { get; set; } = ModUpdateMode.Prompt;
    public IList<string> ModUpdateOptOutModIds { get; set; } = [];
    public HomeBackgroundLogoMode HomeBackgroundLogoMode { get; set; } = HomeBackgroundLogoMode.Square;
    public string? HomeBackgroundCustomLogoPath { get; set; }
    public double HomeBackgroundLogoOpacity { get; set; } = RelicDefaults.HomeBackgroundLogoOpacity;
    public EndpointSettings Endpoints { get; set; } = EndpointSettings.CreateDefaults();
}
