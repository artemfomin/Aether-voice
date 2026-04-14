using System;

namespace VoiceInput.Core.Focus;

/// <summary>
/// Monitors system-wide focus changes to detect text input fields.
/// </summary>
public interface IFocusDetector : IDisposable
{
    void StartMonitoring();
    void StopMonitoring();
    bool IsMonitoring { get; }

    /// <summary>Fires when focus enters a text input field (Edit or Document control).</summary>
    event EventHandler<TextFieldFocusedEventArgs>? TextFieldFocused;

    /// <summary>Fires when focus leaves a text input field.</summary>
    event EventHandler? TextFieldLostFocus;
}
