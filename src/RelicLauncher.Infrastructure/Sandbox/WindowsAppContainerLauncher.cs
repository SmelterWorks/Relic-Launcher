using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

public sealed class WindowsAppContainerLauncher
{
    private readonly WindowsAppContainerAclGranter _aclGranter;
    private readonly ILogger<WindowsAppContainerLauncher> _logger;

    public WindowsAppContainerLauncher(
        WindowsAppContainerAclGranter aclGranter,
        ILogger<WindowsAppContainerLauncher> logger)
    {
        _aclGranter = aclGranter;
        _logger = logger;
    }

    public async Task<Result<SandboxLaunchResult>> LaunchAsync(
        string moniker,
        SandboxPolicy policy,
        SandboxLaunchRequest request,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Result<SandboxLaunchResult>.Failure("AppContainer is only available on Windows.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var profile = await WindowsAppContainerProfile.CreateOrDeriveAsync(moniker).ConfigureAwait(false);
        if (!profile.IsSuccess)
        {
            return Result<SandboxLaunchResult>.Failure(profile.Error!);
        }

        var grant = await _aclGranter.GrantPolicyPathsAsync(
            profile.Value!.Sid,
            policy.PathGrants.ToList(),
            cancellationToken).ConfigureAwait(false);
        if (!grant.IsSuccess)
        {
            return Result<SandboxLaunchResult>.Failure(grant.Error!);
        }

        var capabilities = WindowsAppContainerCapabilities.BuildForKind(policy.Kind);
        var launch = WindowsAppContainerProcess.Start(
            profile.Value!,
            capabilities,
            request);

        if (!launch.IsSuccess)
        {
            return Result<SandboxLaunchResult>.Failure(launch.Error!);
        }

        return Result<SandboxLaunchResult>.Success(new SandboxLaunchResult
        {
            ProcessId = launch.Value!.Id,
            Sandboxed = true,
        });
    }
}

internal sealed class WindowsAppContainerProfile
{
    public required IntPtr Sid { get; init; }

    public static async Task<Result<WindowsAppContainerProfile>> CreateOrDeriveAsync(string moniker)
    {
        await Task.Yield();
        var sid = IntPtr.Zero;
        var hr = NativeMethods.CreateAppContainerProfile(
            moniker,
            moniker,
            moniker,
            IntPtr.Zero,
            0,
            out sid);

        if (hr == NativeMethods.HResultAlreadyExists)
        {
            hr = NativeMethods.DeriveAppContainerSidFromAppContainerName(moniker, out sid);
        }

        if (hr != 0 || sid == IntPtr.Zero)
        {
            return Result<WindowsAppContainerProfile>.Failure(
                $"CreateAppContainerProfile failed: {hr}");
        }

        return Result<WindowsAppContainerProfile>.Success(new WindowsAppContainerProfile { Sid = sid });
    }
}

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
            if (NativeMethods.TryDeriveCapabilitySid(name, out var sid))
            {
                sids.Add(sid);
            }
        }

        return sids.ToArray();
    }
}

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
            var size = IntPtr.Zero;
            NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attributeList = Marshal.AllocHGlobal(size);
            if (!NativeMethods.InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
            {
                return Result<global::System.Diagnostics.Process>.Failure(
                    new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }

            var securityCapabilities = new NativeMethods.SecurityCapabilities
            {
                AppContainerSid = profile.Sid,
                CapabilityCount = capabilitySids.Length,
                Capabilities = capabilitySids.Length == 0
                    ? IntPtr.Zero
                    : Marshal.AllocHGlobal(capabilitySids.Length * Marshal.SizeOf<NativeMethods.SidAndAttributes>()),
            };

            if (capabilitySids.Length > 0)
            {
                var offset = 0;
                foreach (var sid in capabilitySids)
                {
                    var entry = new NativeMethods.SidAndAttributes
                    {
                        Sid = sid,
                        Attributes = NativeMethods.SeGroupEnabled,
                    };
                    Marshal.StructureToPtr(entry, securityCapabilities.Capabilities + offset, false);
                    offset += Marshal.SizeOf<NativeMethods.SidAndAttributes>();
                }
            }

            if (!NativeMethods.UpdateProcThreadAttribute(
                attributeList,
                0,
                NativeMethods.ProcThreadAttributeSecurityCapabilities,
                ref securityCapabilities,
                Marshal.SizeOf<NativeMethods.SecurityCapabilities>(),
                IntPtr.Zero,
                IntPtr.Zero))
            {
                return Result<global::System.Diagnostics.Process>.Failure(
                    new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }

            var startupInfo = new NativeMethods.StartupInfoEx
            {
                StartupInfo = new NativeMethods.StartupInfo
                {
                    cb = Marshal.SizeOf<NativeMethods.StartupInfoEx>(),
                    lpDesktop = "winsta0\\default",
                },
                lpAttributeList = attributeList,
            };

            var commandLine = BuildCommandLine(request.ExecutablePath, request.Arguments);
            var creationFlags = NativeMethods.CreateProcessFlags.ExtendedStartupInfoPresent;
            if (request.RedirectStandardOutput || request.RedirectStandardError)
            {
                creationFlags |= NativeMethods.CreateProcessFlags.RedirectStd;
            }

            if (!NativeMethods.CreateProcessW(
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

            NativeMethods.CloseHandle(processInfo.hThread);
            var process = global::System.Diagnostics.Process.GetProcessById(processInfo.dwProcessId);
            NativeMethods.CloseHandle(processInfo.hProcess);
            return Result<global::System.Diagnostics.Process>.Success(process);
        }
        finally
        {
            if (attributeList != IntPtr.Zero)
            {
                NativeMethods.DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }
        }
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

internal static class NativeMethods
{
    public const int HResultAlreadyExists = unchecked((int)0x800700B7);
    public const int SeGroupEnabled = 0x00000004;
    public const int ProcThreadAttributeSecurityCapabilities = 0x20009;

    [Flags]
    public enum CreateProcessFlags : uint
    {
        ExtendedStartupInfoPresent = 0x00080000,
        RedirectStd = 0x00000100,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SecurityCapabilities
    {
        public IntPtr AppContainerSid;
        public IntPtr Capabilities;
        public int CapabilityCount;
        public int Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SidAndAttributes
    {
        public IntPtr Sid;
        public int Attributes;
    }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
    public static extern int CreateAppContainerProfile(
        string pszAppContainerName,
        string pszDisplayName,
        string pszDescription,
        IntPtr pCapabilities,
        uint dwCapabilityCount,
        out IntPtr ppSidAppContainerSid);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
    public static extern int DeriveAppContainerSidFromAppContainerName(
        string pszAppContainerName,
        out IntPtr ppSidAppContainerSid);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool InitializeProcThreadAttributeList(
        IntPtr lpAttributeList,
        int dwAttributeCount,
        int dwFlags,
        ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList,
        uint dwFlags,
        IntPtr attribute,
        ref SecurityCapabilities lpValue,
        IntPtr cbSize,
        IntPtr lpPreviousValue,
        IntPtr lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CreateProcessW(
        string? lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        CreateProcessFlags dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref StartupInfoEx lpStartupInfo,
        out ProcessInformation lpProcessInformation);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    public static bool TryDeriveCapabilitySid(string name, out IntPtr sid)
    {
        sid = IntPtr.Zero;
        if (!DeriveCapabilitySidsFromName(
            name,
            out var groupSids,
            out var groupCount,
            out var capabilitySids,
            out var capabilityCount))
        {
            return false;
        }

        if (capabilityCount == 0 || capabilitySids == IntPtr.Zero)
        {
            return false;
        }

        sid = Marshal.ReadIntPtr(capabilitySids);
        LocalFree(groupSids);
        LocalFree(capabilitySids);
        return sid != IntPtr.Zero;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeriveCapabilitySidsFromName(
        string CapName,
        out IntPtr CapabilityGroupSids,
        out uint CapabilityGroupSidCount,
        out IntPtr CapabilitySids,
        out uint CapabilitySidCount);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
