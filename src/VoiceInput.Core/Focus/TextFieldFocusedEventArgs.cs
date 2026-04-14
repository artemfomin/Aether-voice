using System;

namespace VoiceInput.Core.Focus;

/// <summary>
/// Event args when a text input field receives focus.
/// </summary>
public sealed class TextFieldFocusedEventArgs : EventArgs
{
    public required FocusRect CaretBounds { get; init; }
    public required string ProcessName { get; init; }
    public required string WindowTitle { get; init; }
    public bool IsPassword { get; init; }
    public bool IsReadOnly { get; init; }
}
