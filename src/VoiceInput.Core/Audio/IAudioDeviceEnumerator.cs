using System;
using System.Collections.Generic;

namespace VoiceInput.Core.Audio;

/// <summary>
/// Enumerates audio input devices and monitors for hot-plug events.
/// </summary>
public interface IAudioDeviceEnumerator : IDisposable
{
    /// <summary>Returns all available audio input (capture) devices.</summary>
    List<AudioDevice> GetDevices();

    /// <summary>Fires when a device is added, removed, or changed state.</summary>
    event EventHandler? DeviceStateChanged;
}
