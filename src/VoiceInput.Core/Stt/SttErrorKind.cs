namespace VoiceInput.Core.Stt;

/// <summary>
/// Categorises the kind of failure that occurred during speech-to-text processing.
/// </summary>
public enum SttErrorKind
{
    /// <summary>The request timed out before a response was received.</summary>
    Timeout,

    /// <summary>Authentication or authorisation failed (e.g. invalid API key).</summary>
    AuthError,

    /// <summary>The audio format is not supported by the provider.</summary>
    FormatError,

    /// <summary>The provider is not registered, not reachable, or not configured.</summary>
    Unavailable,

    /// <summary>An unexpected error occurred that does not fit another category.</summary>
    Unknown,
}
