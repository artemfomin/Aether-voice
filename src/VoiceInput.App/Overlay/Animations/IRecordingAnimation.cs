using System.Windows;

namespace VoiceInput.App.Overlay.Animations;

/// <summary>
/// Interface for recording indicator animations inside the floating island.
/// </summary>
public interface IRecordingAnimation
{
    /// <summary>Start the animation loop.</summary>
    void Start();

    /// <summary>Stop the animation loop.</summary>
    void Stop();

    /// <summary>Update with current audio amplitude [0.0, 1.0].</summary>
    void UpdateAmplitude(float amplitude);

    /// <summary>The WPF visual element to embed in the overlay.</summary>
    UIElement Visual { get; }
}
