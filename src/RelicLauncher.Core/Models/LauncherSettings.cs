namespace RelicLauncher.Core.Models;

public sealed class LauncherSettings
{
    public const string DefaultThemeId = "relic-default";

    public string SelectedThemeId { get; set; } = DefaultThemeId;
    public string? GameInstallPath { get; set; }
    public bool ConfirmBeforeExit { get; set; }
    public HomeBackgroundLogoMode HomeBackgroundLogoMode { get; set; } = HomeBackgroundLogoMode.Square;
    public string? HomeBackgroundCustomLogoPath { get; set; }
    public double HomeBackgroundLogoOpacity { get; set; } = 0.12;
}
