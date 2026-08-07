using System.ComponentModel;
using System.Runtime.InteropServices;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

internal static class WindowsAppContainerProcess
{
    public static Result<global::System.Diagnostics.Process> Start(
        WindowsAppContainerProfile profile,
        IntPtr[] capabilitySids,
        SandboxLaunchRequest request)
    {
        var attributeList = IntPtr.Zero;
        try
        {
            if (!TryCreateAttributeList(out attributeList, profile, capabilitySids, out var securityCapabilities))
            {
                return Result<global::System.Diagnostics.Process>.Failure(
                    new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }

            var startupInfo = CreateStartupInfo(attributeList);
            var commandLine = BuildCommandLine(request.ExecutablePath, request.Arguments);
            var creationFlags = WindowsAppContainerNativeMethods.CreateProcessFlags.ExtendedStartupInfoPresent;
            if (request.RedirectStandardOutput || request.RedirectStandardError)
            {
                creationFlags |= WindowsAppContainerNativeMethods.CreateProcessFlags.RedirectStd;
            }

            if (!WindowsAppContainerNativeMethods.CreateProcessW(
                null,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                request.RedirectStandardInput || request.RedirectStandardOutput || request.RedirectStandardError,
                creationFlags,
                IntPtr.Zero,
                request.WorkingDirectory,
                ref startupInfo,
                out var processInfo))
            {
                return Result<global::System.Diagnostics.Process>.Failure(
                    new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }

            WindowsAppContainerNativeMethods.CloseHandle(processInfo.hThread);
            var process = global::System.Diagnostics.Process.GetProcessById(processInfo.dwProcessId);
            WindowsAppContainerNativeMethods.CloseHandle(processInfo.hProcess);
            return Result<global::System.Diagnostics.Process>.Success(process);
        }
        finally
        {
            if (attributeList != IntPtr.Zero)
            {
                WindowsAppContainerNativeMethods.DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }
        }
    }

    private static bool TryCreateAttributeList(
        out IntPtr attributeList,
        WindowsAppContainerProfile profile,
        IntPtr[] capabilitySids,
        out WindowsAppContainerNativeMethods.SecurityCapabilities securityCapabilities)
    {
        attributeList = IntPtr.Zero;
        securityCapabilities = default;

        var size = IntPtr.Zero;
        WindowsAppContainerNativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        attributeList = Marshal.AllocHGlobal(size);
        if (!WindowsAppContainerNativeMethods.InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
        {
            return false;
        }

        securityCapabilities = new WindowsAppContainerNativeMethods.SecurityCapabilities
        {
            AppContainerSid = profile.Sid,
            CapabilityCount = capabilitySids.Length,
            Capabilities = capabilitySids.Length == 0
                ? IntPtr.Zero
                : Marshal.AllocHGlobal(capabilitySids.Length * Marshal.SizeOf<WindowsAppContainerNativeMethods.SidAndAttributes>()),
        };

        if (capabilitySids.Length > 0)
        {
            var offset = 0;
            foreach (var sid in capabilitySids)
            {
                var entry = new WindowsAppContainerNativeMethods.SidAndAttributes
                {
                    Sid = sid,
                    Attributes = WindowsAppContainerNativeMethods.SeGroupEnabled,
                };
                Marshal.StructureToPtr(entry, securityCapabilities.Capabilities + offset, false);
                offset += Marshal.SizeOf<WindowsAppContainerNativeMethods.SidAndAttributes>();
            }
        }

        return WindowsAppContainerNativeMethods.UpdateProcThreadAttribute(
            attributeList,
            0,
            WindowsAppContainerNativeMethods.ProcThreadAttributeSecurityCapabilities,
            ref securityCapabilities,
            Marshal.SizeOf<WindowsAppContainerNativeMethods.SecurityCapabilities>(),
            IntPtr.Zero,
            IntPtr.Zero);
    }

    private static WindowsAppContainerNativeMethods.StartupInfoEx CreateStartupInfo(IntPtr attributeList)
    {
        return new WindowsAppContainerNativeMethods.StartupInfoEx
        {
            StartupInfo = new WindowsAppContainerNativeMethods.StartupInfo
            {
                cb = Marshal.SizeOf<WindowsAppContainerNativeMethods.StartupInfoEx>(),
                lpDesktop = "winsta0\\default",
            },
            lpAttributeList = attributeList,
        };
    }

    private static string BuildCommandLine(string executable, IList<string> arguments)
    {
        var parts = new List<string> { Quote(executable) };
        foreach (var arg in arguments)
        {
            parts.Add(Quote(arg));
        }

        return string.Join(' ', parts);
    }

    private static string Quote(string value)
    {
        if (value.Contains(' ') || value.Contains('"'))
        {
            return '"' + value.Replace("\"", "\\\"") + '"';
        }

        return value;
    }
}
