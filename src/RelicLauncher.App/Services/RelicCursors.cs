using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace RelicLauncher.App.Services;

public static class RelicCursors
{
    public const string PointerUri = "avares://RelicLauncher.App/Assets/Cursors/relic-pointer-32.png";

    public static readonly PixelPoint PointerHotSpot = new(2, 2);

    private static Cursor? _pointer;
    private static readonly Lock Gate = new();

    public static Cursor? TryGetPointer()
    {
        if (_pointer is not null)
        {
            return _pointer;
        }

        lock (Gate)
        {
            if (_pointer is not null)
            {
                return _pointer;
            }

            try
            {
                using var stream = AssetLoader.Open(new Uri(PointerUri));
                var bitmap = new Bitmap(stream);
                _pointer = new Cursor(bitmap, PointerHotSpot);
                return _pointer;
            }
            catch
            {
                return null;
            }
        }
    }
}
