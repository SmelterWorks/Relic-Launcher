using System.Runtime.InteropServices;

namespace RelicLauncher.Infrastructure.Sandbox;

internal static class WindowsAppContainerNativeMethods
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
