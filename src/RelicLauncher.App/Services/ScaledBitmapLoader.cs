using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace RelicLauncher.App.Services;

public static class ScaledBitmapLoader
{
    public static Bitmap? FromStream(Stream stream, int maxWidth)
    {
        if (maxWidth <= 0)
        {
            return new Bitmap(stream);
        }

        return Bitmap.DecodeToWidth(stream, maxWidth, BitmapInterpolationMode.HighQuality);
    }

    public static Bitmap? FromBytes(byte[] bytes, int maxWidth)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return FromStream(stream, maxWidth);
    }

    public static Bitmap? FromFile(string path, int maxWidth)
    {
        using var stream = File.OpenRead(path);
        return FromStream(stream, maxWidth);
    }

    public static Bitmap? FromAssetUri(string avaresUri, int maxWidth)
    {
        using var stream = AssetLoader.Open(new Uri(avaresUri));
        return FromStream(stream, maxWidth);
    }
}
