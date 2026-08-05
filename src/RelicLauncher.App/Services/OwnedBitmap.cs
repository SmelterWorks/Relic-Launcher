using Avalonia.Media.Imaging;

namespace RelicLauncher.App.Services;

public static class OwnedBitmap
{
    public static void DisposeIfOwned(Bitmap? bitmap)
    {
        if (bitmap is null || ReferenceEquals(bitmap, ModIconAssets.Default))
        {
            return;
        }

        bitmap.Dispose();
    }
}
