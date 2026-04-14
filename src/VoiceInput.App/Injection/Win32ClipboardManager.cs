using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using VoiceInput.Core.Injection;

namespace VoiceInput.App.Injection;

/// <summary>
/// Windows clipboard manager that saves and restores a whitelisted set of
/// clipboard formats using Win32 P/Invoke.
/// </summary>
/// <remarks>
/// <para>
/// <b>Whitelisted formats</b>: CF_UNICODETEXT, CF_TEXT, CF_BITMAP, CF_HDROP.
/// CF_BITMAP is a GDI handle rather than a global memory block, so its
/// byte content cannot be copied; the format is acknowledged but not
/// byte-level preserved.
/// </para>
/// <para>
/// <b>Try-finally contract</b>: every public method that opens the clipboard
/// wraps its work in a <c>try/finally</c> block that always calls
/// <see cref="ClipboardNativeMethods.CloseClipboard"/>, ensuring the clipboard
/// is never left open.  Callers are responsible for wrapping
/// <see cref="SetText"/> + injection + <see cref="RestoreState"/> in their own
/// <c>try/finally</c> so the clipboard state is always restored.
/// </para>
/// <para>
/// <b>Thread affinity</b>: Win32 clipboard operations do not require an STA
/// thread; this class may be called from any thread.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class Win32ClipboardManager : IClipboardManager
{
    // Formats examined during save/restore.  CF_BITMAP is in the list so it
    // is acknowledged during save; byte-level copy is skipped for GDI handles.
    private static readonly uint[] WhitelistedFormats =
    [
        ClipboardNativeMethods.CF_UNICODETEXT,
        ClipboardNativeMethods.CF_TEXT,
        ClipboardNativeMethods.CF_BITMAP,
        ClipboardNativeMethods.CF_HDROP,
    ];

    /// <summary>Maximum total wait time for <see cref="TryOpenClipboard"/>.</summary>
    private const int MaxOpenRetries = 10;

    /// <summary>Sleep between <see cref="ClipboardNativeMethods.OpenClipboard"/> retries (ms).</summary>
    private const int RetryDelayMs = 50; // 10 × 50 ms = 500 ms total

    private Dictionary<uint, byte[]> _savedState = new();

    // ── IClipboardManager ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void SaveState()
    {
        _savedState.Clear();

        if (!TryOpenClipboard())
            return;

        try
        {
            CaptureWhitelistedFormats();
        }
        finally
        {
            ClipboardNativeMethods.CloseClipboard();
        }
    }

    /// <inheritdoc/>
    public void RestoreState()
    {
        if (_savedState.Count == 0)
            return;

        if (!TryOpenClipboard())
            return;

        try
        {
            ClipboardNativeMethods.EmptyClipboard();
            WriteAllSavedFormats();
        }
        finally
        {
            ClipboardNativeMethods.CloseClipboard();
        }
    }

    /// <inheritdoc/>
    public void SetText(string text)
    {
        if (!TryOpenClipboard())
            return;

        try
        {
            ClipboardNativeMethods.EmptyClipboard();
            WriteUnicodeText(text);
        }
        finally
        {
            ClipboardNativeMethods.CloseClipboard();
        }
    }

    /// <inheritdoc/>
    public string? GetText()
    {
        // Fast path: avoid opening the clipboard when Unicode text is absent.
        if (!ClipboardNativeMethods.IsClipboardFormatAvailable(ClipboardNativeMethods.CF_UNICODETEXT))
            return null;

        if (!TryOpenClipboard())
            return null;

        try
        {
            return ReadUnicodeText();
        }
        finally
        {
            ClipboardNativeMethods.CloseClipboard();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to open the clipboard up to <see cref="MaxOpenRetries"/> times,
    /// sleeping <see cref="RetryDelayMs"/> ms between attempts.
    /// Returns <see langword="true"/> when the clipboard was opened successfully.
    /// </summary>
    private static bool TryOpenClipboard()
    {
        for (var attempt = 0; attempt < MaxOpenRetries; attempt++)
        {
            if (ClipboardNativeMethods.OpenClipboard(IntPtr.Zero))
                return true;

            Thread.Sleep(RetryDelayMs);
        }

        return false;
    }

    private void CaptureWhitelistedFormats()
    {
        foreach (var format in WhitelistedFormats)
        {
            var bytes = ReadFormatBytes(format);
            if (bytes is not null)
                _savedState[format] = bytes;
        }
    }

    private void WriteAllSavedFormats()
    {
        foreach (var (format, bytes) in _savedState)
            WriteFormatBytes(format, bytes);
    }

    /// <summary>
    /// Reads the raw bytes of a global-memory clipboard format handle.
    /// Returns <see langword="null"/> when the format is absent or uses a
    /// non-memory handle (e.g. CF_BITMAP / HBITMAP).
    /// The clipboard must be open.
    /// </summary>
    private static byte[]? ReadFormatBytes(uint format)
    {
        // CF_BITMAP is an HBITMAP (GDI handle), not a global memory block.
        // Passing an HBITMAP to GlobalLock is undefined behaviour; skip it.
        if (format == ClipboardNativeMethods.CF_BITMAP)
            return null;

        var hData = ClipboardNativeMethods.GetClipboardData(format);
        if (hData == IntPtr.Zero)
            return null;

        var size = ClipboardNativeMethods.GlobalSize(hData);
        if (size == 0)
            return null;

        var ptr = ClipboardNativeMethods.GlobalLock(hData);
        if (ptr == IntPtr.Zero)
            return null;

        try
        {
            var bytes = new byte[(int)size];
            Marshal.Copy(ptr, bytes, 0, (int)size);
            return bytes;
        }
        finally
        {
            ClipboardNativeMethods.GlobalUnlock(hData);
        }
    }

    /// <summary>
    /// Allocates a moveable global memory block, copies <paramref name="bytes"/>
    /// into it, and registers it with the open clipboard via SetClipboardData.
    /// On success the OS owns the handle; on failure the handle is freed.
    /// The clipboard must be open.
    /// </summary>
    private static void WriteFormatBytes(uint format, byte[] bytes)
    {
        var hMem = ClipboardNativeMethods.GlobalAlloc(
            ClipboardNativeMethods.GmemMoveable,
            (nuint)bytes.Length);

        if (hMem == IntPtr.Zero)
            return;

        if (!TryCopyBytesToHandle(hMem, bytes))
        {
            ClipboardNativeMethods.GlobalFree(hMem);
            return;
        }

        var placed = ClipboardNativeMethods.SetClipboardData(format, hMem);
        if (placed == IntPtr.Zero)
        {
            // SetClipboardData failed — reclaim the memory we allocated.
            ClipboardNativeMethods.GlobalFree(hMem);
        }
        // On success the OS takes ownership; we must NOT free hMem.
    }

    /// <summary>
    /// Locks <paramref name="hMem"/>, copies <paramref name="bytes"/> into it,
    /// then unlocks it. Returns <see langword="false"/> if locking fails.
    /// </summary>
    private static bool TryCopyBytesToHandle(IntPtr hMem, byte[] bytes)
    {
        var ptr = ClipboardNativeMethods.GlobalLock(hMem);
        if (ptr == IntPtr.Zero)
            return false;

        try
        {
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return true;
        }
        finally
        {
            ClipboardNativeMethods.GlobalUnlock(hMem);
        }
    }

    /// <summary>
    /// Writes <paramref name="text"/> to the open clipboard as CF_UNICODETEXT
    /// with a null terminator, using a moveable global memory block.
    /// </summary>
    private static void WriteUnicodeText(string text)
    {
        // CF_UNICODETEXT requires a null-terminated UTF-16 string.
        var bytes = Encoding.Unicode.GetBytes(text + '\0');
        WriteFormatBytes(ClipboardNativeMethods.CF_UNICODETEXT, bytes);
    }

    /// <summary>
    /// Reads the CF_UNICODETEXT data from the open clipboard.
    /// Returns <see langword="null"/> when the handle is unavailable.
    /// </summary>
    private static string? ReadUnicodeText()
    {
        var hData = ClipboardNativeMethods.GetClipboardData(
            ClipboardNativeMethods.CF_UNICODETEXT);

        if (hData == IntPtr.Zero)
            return null;

        var ptr = ClipboardNativeMethods.GlobalLock(hData);
        if (ptr == IntPtr.Zero)
            return null;

        try
        {
            return Marshal.PtrToStringUni(ptr);
        }
        finally
        {
            ClipboardNativeMethods.GlobalUnlock(hData);
        }
    }
}
