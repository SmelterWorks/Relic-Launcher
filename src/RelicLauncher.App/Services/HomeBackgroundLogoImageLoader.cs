using Avalonia.Media.Imaging;
using RelicLauncher.Core.Constants;

namespace RelicLauncher.App.Services;

public static class HomeBackgroundLogoImageLoader
{
    public static Bitmap? Load(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        try
        {
            if (source.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                return ScaledBitmapLoader.FromAssetUri(source, RelicDefaults.DecodeWidthHomeLogo);
            }

            if (File.Exists(source))
            {
                return ScaledBitmapLoader.FromFile(source, RelicDefaults.DecodeWidthHomeLogo);
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }
}
