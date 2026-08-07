using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

internal static class WindowsAppContainerCapabilities
{
    public static IntPtr[] BuildForKind(SandboxKind kind)
    {
        var names = kind switch
        {
            SandboxKind.Launcher => new[] { "internetClient", "privateNetworkClientServer" },
            SandboxKind.GameClient => new[] { "internetClient", "privateNetworkClientServer", "codeGeneration" },
            SandboxKind.DedicatedServer => new[] { "internetClientServer", "privateNetworkClientServer" },
            _ => new[] { "internetClient" },
        };

        var sids = new List<IntPtr>();
        foreach (var name in names)
        {
            if (WindowsAppContainerNativeMethods.TryDeriveCapabilitySid(name, out var sid))
            {
                sids.Add(sid);
            }
        }

        return sids.ToArray();
    }
}
