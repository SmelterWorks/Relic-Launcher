using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Sandbox;

internal sealed class WindowsAppContainerProfile
{
    public required IntPtr Sid { get; init; }

    public static async Task<Result<WindowsAppContainerProfile>> CreateOrDeriveAsync(string moniker)
    {
        await Task.Yield();
        var sid = IntPtr.Zero;
        var hr = WindowsAppContainerNativeMethods.CreateAppContainerProfile(
            moniker,
            moniker,
            moniker,
            IntPtr.Zero,
            0,
            out sid);

        if (hr == WindowsAppContainerNativeMethods.HResultAlreadyExists)
        {
            hr = WindowsAppContainerNativeMethods.DeriveAppContainerSidFromAppContainerName(moniker, out sid);
        }

        if (hr != 0 || sid == IntPtr.Zero)
        {
            return Result<WindowsAppContainerProfile>.Failure(
                $"CreateAppContainerProfile failed: {hr}");
        }

        return Result<WindowsAppContainerProfile>.Success(new WindowsAppContainerProfile { Sid = sid });
    }
}
