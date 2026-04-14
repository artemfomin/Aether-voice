namespace VoiceInput.Core.Injection;

/// <summary>
/// Manages clipboard state to support the text-injection workflow:
/// save the existing clipboard, place transcribed text on it, inject via
/// Ctrl+V / Ctrl+Shift+V, then restore the original content.
/// </summary>
/// <remarks>
/// Callers are responsible for calling <see cref="RestoreState"/> in a
/// <c>try/finally</c> block so the clipboard is always restored even when
/// an exception occurs during injection.
/// </remarks>
public interface IClipboardManager
{
    /// <summary>
    /// Captures the current clipboard content for later restoration.
    /// Only whitelisted formats (CF_UNICODETEXT, CF_TEXT, CF_BITMAP, CF_HDROP)
    /// are preserved; all other formats are ignored.
    /// </summary>
    void SaveState();

    /// <summary>
    /// Writes the previously saved clipboard content back to the system clipboard.
    /// If <see cref="SaveState"/> was never called the clipboard is left unchanged.
    /// </summary>
    void RestoreState();

    /// <summary>
    /// Replaces the clipboard content with <paramref name="text"/> encoded as
    /// CF_UNICODETEXT so it can be pasted into the target application.
    /// </summary>
    /// <param name="text">The text to place on the clipboard.</param>
    void SetText(string text);

    /// <summary>
    /// Returns the current CF_UNICODETEXT clipboard value, or
    /// <see langword="null"/> when no Unicode text is available.
    /// </summary>
    string? GetText();
}
