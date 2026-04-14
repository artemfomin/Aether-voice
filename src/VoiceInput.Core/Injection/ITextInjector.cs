namespace VoiceInput.Core.Injection;

/// <summary>
/// Injects text into the currently focused window using the most appropriate
/// strategy for that window type (terminal vs. regular application).
/// </summary>
public interface ITextInjector
{
    /// <summary>
    /// Injects <paramref name="text"/> into <paramref name="targetWindow"/>.
    /// </summary>
    /// <remarks>
    /// For keyboard-based strategies (<see cref="InjectionStrategy.CtrlV"/> /
    /// <see cref="InjectionStrategy.CtrlShiftV"/>) the caller is responsible for
    /// placing <paramref name="text"/> in the clipboard before calling this method.
    /// The <paramref name="text"/> value is used directly only by the
    /// <see cref="InjectionStrategy.UiaSetValue"/> strategy.
    /// </remarks>
    /// <param name="text">
    /// The text to inject.  Must not be <see langword="null"/> or empty.
    /// </param>
    /// <param name="targetWindow">
    /// Handle of the target window.  Pass <see cref="IntPtr.Zero"/> to target
    /// whichever window is currently in the foreground.
    /// </param>
    /// <returns>The outcome of the injection attempt.</returns>
    Task<InjectionResult> InjectTextAsync(string text, IntPtr targetWindow);
}
