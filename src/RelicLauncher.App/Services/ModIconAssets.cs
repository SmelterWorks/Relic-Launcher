using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace RelicLauncher.App.Services;

public static class ModIconAssets
{
    public const string DefaultLogoUri = "avares://RelicLauncher.App/Assets/Mods/mod-default.png";
    public const string SourceUrl = "https://mods.vintagestory.at/web/img/mod-default.png";

    private static readonly Lazy<Bitmap> DefaultLogo = new(LoadDefault);

    public static Bitmap Default => DefaultLogo.Value;

    private static Bitmap LoadDefault()
    {
        using var stream = AssetLoader.Open(new Uri(DefaultLogoUri));
        return new Bitmap(stream);
    }
}
