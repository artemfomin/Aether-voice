namespace VoiceInput.Core.Injection;

/// <summary>
/// Describes the outcome of a text injection attempt.
/// </summary>
public enum InjectionResult
{
    /// <summary>Text was successfully injected into the target window.</summary>
    Success,

    /// <summary>
    /// Injection was skipped because the target process runs at a higher
    /// integrity level (UAC-elevated) and cannot receive simulated input.
    /// </summary>
    SkippedElevated,

    /// <summary>
    /// Injection was skipped because the target field is a password field
    /// and injection is prohibited for security reasons.
    /// </summary>
    SkippedPassword,

    /// <summary>Injection failed due to an unexpected error.</summary>
    Error,
}
