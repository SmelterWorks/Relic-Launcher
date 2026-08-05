using RelicLauncher.Core.Models;

namespace RelicLauncher.App.Services;

public static class HomeBackgroundLogoResolver
{
    public const string SquareLogoUri = "avares://RelicLauncher.App/Assets/Branding/vs-logo-square-ui.png";
    public const string BannerLogoUri = "avares://RelicLauncher.App/Assets/Branding/vs-logo-banner.png";

    public static HomeBackgroundLogoState Resolve(LauncherSettings settings)
    {
        var opacity = Math.Clamp(settings.HomeBackgroundLogoOpacity, 0.02, 1.0);

        return settings.HomeBackgroundLogoMode switch
        {
            HomeBackgroundLogoMode.None => new HomeBackgroundLogoState(false, null, opacity),
            HomeBackgroundLogoMode.Square => new HomeBackgroundLogoState(true, SquareLogoUri, opacity),
            HomeBackgroundLogoMode.Banner => new HomeBackgroundLogoState(true, BannerLogoUri, opacity),
            HomeBackgroundLogoMode.Custom => ResolveCustom(settings, opacity),
            _ => new HomeBackgroundLogoState(false, null, opacity),
        };
    }

    private static HomeBackgroundLogoState ResolveCustom(LauncherSettings settings, double opacity)
    {
        if (!string.IsNullOrWhiteSpace(settings.HomeBackgroundCustomLogoPath) &&
            File.Exists(settings.HomeBackgroundCustomLogoPath))
        {
            return new HomeBackgroundLogoState(true, settings.HomeBackgroundCustomLogoPath, opacity);
        }

        return new HomeBackgroundLogoState(false, null, opacity);
    }
}
