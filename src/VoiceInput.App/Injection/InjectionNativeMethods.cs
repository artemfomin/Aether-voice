using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VoiceInput.App.Injection;

/// <summary>
/// P/Invoke declarations for Win32 APIs used by <see cref="SendInputTextInjector"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class InjectionNativeMethods
{
    // ── Input type constant ───────────────────────────────────────────────────

    internal const uint InputKeyboard = 1;

    // ── KEYBDINPUT flag constants ─────────────────────────────────────────────

    /// <summary>Key-up event (KEYEVENTF_KEYUP).</summary>
    internal const uint KeyeventfKeyup = 0x0002;

    /// <summary>Unicode character scan code (KEYEVENTF_UNICODE).</summary>
    internal const uint KeyeventfUnicode = 0x0004;

    // ── Virtual key codes ─────────────────────────────────────────────────────

    internal const ushort VkControl = 0x11;
    internal const ushort VkShift   = 0x10;
    internal const ushort VkMenu    = 0x12; // Alt
    internal const ushort VkLWin    = 0x5B; // Left Windows
    internal const ushort VkV       = 0x56;

    // ── Process / token access rights ────────────────────────────────────────

    internal const uint ProcessQueryLimitedInformation = 0x1000;
    internal const uint TokenQuery = 0x0008;

    // ── GetClassName buffer capacity ──────────────────────────────────────────

    internal const int ClassNameCapacity = 256;

    // ── user32.dll ────────────────────────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(
        uint nInputs,
        INPUT[] pInputs,
        int cbSize);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern int GetClassName(
        IntPtr hWnd,
        System.Text.StringBuilder lpClassName,
        int nMaxCount);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint lpdwProcessId);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    // ── kernel32.dll ──────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(
        uint dwDesiredAccess,
        bool bInheritHandle,
        uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetCurrentProcess();

    // ── advapi32.dll ──────────────────────────────────────────────────────────

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool OpenProcessToken(
        IntPtr processHandle,
        uint desiredAccess,
        out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        TokenInformationClass tokenInformationClass,
        IntPtr tokenInformation,
        uint tokenInformationLength,
        out uint returnLength);

    /// <summary>
    /// Returns a pointer to the sub-authority count byte inside a SID.
    /// Caller must NOT free the returned pointer — it points inside the SID buffer.
    /// </summary>
    [DllImport("advapi32.dll")]
    internal static extern IntPtr GetSidSubAuthorityCount(IntPtr pSid);

    /// <summary>
    /// Returns a pointer to the specified sub-authority DWORD inside a SID.
    /// Caller must NOT free the returned pointer — it points inside the SID buffer.
    /// </summary>
    [DllImport("advapi32.dll")]
    internal static extern IntPtr GetSidSubAuthority(IntPtr pSid, uint nSubAuthority);

    // ── Structs ───────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT  Mi;
        [FieldOffset(0)] public KEYBDINPUT  Ki;
        [FieldOffset(0)] public HARDWAREINPUT Hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int    Dx;
        public int    Dy;
        public uint   MouseData;
        public uint   Flags;
        public uint   Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort VirtualKeyCode;
        public ushort ScanCode;
        public uint   Flags;
        public uint   Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint   Message;
        public ushort ParamLow;
        public ushort ParamHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TOKEN_MANDATORY_LABEL
    {
        public SID_AND_ATTRIBUTES Label;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SID_AND_ATTRIBUTES
    {
        public IntPtr Sid;
        public uint   Attributes;
    }

    // ── Enums ─────────────────────────────────────────────────────────────────

    internal enum TokenInformationClass
    {
        TokenIntegrityLevel = 25,
    }
}
