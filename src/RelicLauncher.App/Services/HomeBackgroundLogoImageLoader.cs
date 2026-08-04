using Avalonia.Media.Imaging;
using Avalonia.Platform;

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
                using var stream = AssetLoader.Open(new Uri(source));
                return new Bitmap(stream);
            }

            if (File.Exists(source))
            {
                return new Bitmap(source);
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }
}
