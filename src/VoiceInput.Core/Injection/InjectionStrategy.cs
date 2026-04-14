namespace VoiceInput.Core.Injection;

/// <summary>
/// Strategy used to inject text into a target window.
/// </summary>
public enum InjectionStrategy
{
    /// <summary>
    /// Ctrl+V keyboard shortcut — standard paste for most GUI applications.
    /// Requires the text to already be present in the clipboard.
    /// </summary>
    CtrlV,

    /// <summary>
    /// Ctrl+Shift+V keyboard shortcut — paste for terminal emulators that
    /// reserve Ctrl+V for control characters.
    /// Requires the text to already be present in the clipboard.
    /// </summary>
    CtrlShiftV,

    /// <summary>
    /// UI Automation SetValue — writes directly to the control's value
    /// property without using the clipboard.  Suitable for accessibility-
    /// enabled text controls that expose a ValuePattern.
    /// </summary>
    UiaSetValue,
}
