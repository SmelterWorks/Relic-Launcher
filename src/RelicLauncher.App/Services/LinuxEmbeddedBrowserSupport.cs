using System.Runtime.InteropServices;

namespace RelicLauncher.App.Services;

internal static class LinuxEmbeddedBrowserSupport
{
    private static readonly string[] WpeCandidates =
    [
        "libwpe-1",
        "wpe-1",
    ];

    private static readonly string[] WebKitGtkCandidates =
    [
        "webkit2gtk-4.1",
        "libwebkit2gtk-4.1",
        "webkit2gtk-4.0",
        "libwebkit2gtk-4.0",
        "webkit2gtk",
        "libwebkit2gtk",
    ];

    public static bool IsLikelyAvailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return true;
        }

        return IsWpeAvailable() || IsWebKitGtkAvailable();
    }

    public static bool IsWpeAvailable() => TryLoadAny(WpeCandidates);

    public static bool IsWebKitGtkAvailable() => TryLoadAny(WebKitGtkCandidates);

    public static bool PreferWebKitGtkInstead => IsWebKitGtkAvailable() && !IsWpeAvailable();

    private static bool TryLoadAny(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            if (NativeLibrary.TryLoad(name, typeof(LinuxEmbeddedBrowserSupport).Assembly, null, out var handle))
            {
                NativeLibrary.Free(handle);
                return true;
            }
        }

        return false;
    }
}
