using System.Runtime.InteropServices;

namespace VoiceInput.App.Native;

/// <summary>
/// P/Invoke declarations for Win32 APIs used by the application.
/// </summary>
internal static class NativeMethods
{
    // ── Efficiency Mode (Windows 11) ─────────────────────────────────────────

    private const int ProcessPowerThrottling = 4;
    private const uint ProcessPowerThrottlingExecutionSpeed = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessInformation(
        nint hProcess,
        int processInformationClass,
        nint processInformation,
        uint processInformationSize);

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    /// <summary>
    /// Enables Windows 11 Efficiency Mode for the current process.
    /// Activates power throttling to reduce CPU and power usage for background apps.
    /// </summary>
    internal static void EnableEfficiencyMode()
    {
        var state = new ProcessPowerThrottlingState
        {
            Version = 1,
            ControlMask = ProcessPowerThrottlingExecutionSpeed,
            StateMask = ProcessPowerThrottlingExecutionSpeed,
        };

        var size = (uint)Marshal.SizeOf<ProcessPowerThrottlingState>();
        var ptr = Marshal.AllocHGlobal((int)size);
        try
        {
            Marshal.StructureToPtr(state, ptr, false);
            SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ptr, size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
