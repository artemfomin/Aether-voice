namespace VoiceInput.Core.Stt;

/// <summary>
/// Typed exception thrown when a speech-to-text operation fails.
/// </summary>
public sealed class SttException : Exception
{
    /// <summary>Gets the category of the failure.</summary>
    public SttErrorKind Kind { get; }

    public SttException(SttErrorKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public SttException(SttErrorKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }
}
