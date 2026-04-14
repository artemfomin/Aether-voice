using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VoiceInput.App.Injection;

/// <summary>
/// P/Invoke declarations for Win32 clipboard APIs used by
/// <see cref="Win32ClipboardManager"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ClipboardNativeMethods
{
    // ── Standard clipboard format identifiers ─────────────────────────────────

    /// <summary>ANSI text (null-terminated). Global memory handle.</summary>
    internal const uint CF_TEXT        = 1;

    /// <summary>Device-dependent bitmap (HBITMAP). GDI handle — not a global memory block.</summary>
    internal const uint CF_BITMAP      = 2;

    /// <summary>Unicode text (null-terminated). Global memory handle.</summary>
    internal const uint CF_UNICODETEXT = 13;

    /// <summary>Shell file-drop list (DROPFILES). Global memory handle.</summary>
    internal const uint CF_HDROP       = 15;

    // ── GlobalAlloc flag ──────────────────────────────────────────────────────

    /// <summary>Allocates moveable memory (GMEM_MOVEABLE = 0x0002).</summary>
    internal const uint GmemMoveable = 0x0002;

    // ── user32.dll ────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the clipboard for examination and prevents other applications
    /// from modifying the clipboard content.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool OpenClipboard(IntPtr hWndNewOwner);

    /// <summary>
    /// Closes the clipboard after it has been examined or changed.
    /// Must be called after every successful <see cref="OpenClipboard"/>.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseClipboard();

    /// <summary>
    /// Empties the clipboard and frees handles to data in the clipboard.
    /// The clipboard must be open when this is called.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EmptyClipboard();

    /// <summary>
    /// Determines whether the clipboard contains data in the specified format.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsClipboardFormatAvailable(uint format);

    /// <summary>
    /// Retrieves data from the clipboard in the specified format.
    /// The clipboard must be open. Returns <see cref="IntPtr.Zero"/> on failure.
    /// </summary>
    [DllImport("user32.dll")]
    internal static extern IntPtr GetClipboardData(uint uFormat);

    /// <summary>
    /// Places data on the clipboard in the specified format.
    /// The clipboard must be open. On success the OS takes ownership of
    /// <paramref name="hMem"/>; the caller must NOT free it.
    /// Returns <see cref="IntPtr.Zero"/> on failure (caller must then free
    /// <paramref name="hMem"/>).
    /// </summary>
    [DllImport("user32.dll")]
    internal static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    // ── kernel32.dll ──────────────────────────────────────────────────────────

    /// <summary>
    /// Allocates the specified number of bytes from the heap.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GlobalAlloc(uint uFlags, nuint dwBytes);

    /// <summary>
    /// Locks a global memory object and returns a pointer to its first byte.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GlobalLock(IntPtr hMem);

    /// <summary>
    /// Decrements the lock count on a global memory object.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalUnlock(IntPtr hMem);

    /// <summary>
    /// Frees the specified global memory object.
    /// Returns <see cref="IntPtr.Zero"/> on success.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GlobalFree(IntPtr hMem);

    /// <summary>
    /// Retrieves the current size of the specified global memory object, in bytes.
    /// Returns 0 if the handle is invalid or not a global memory object.
    /// </summary>
    [DllImport("kernel32.dll")]
    internal static extern nuint GlobalSize(IntPtr hMem);
}
