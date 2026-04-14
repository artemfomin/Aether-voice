using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using VoiceInput.Core.Audio;

namespace VoiceInput.App.Audio;

/// <summary>
/// Enumerates WASAPI capture (microphone) devices via NAudio MMDeviceEnumerator.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WasapiDeviceEnumerator : IAudioDeviceEnumerator
{
    private readonly MMDeviceEnumerator _enumerator;
    private bool _disposed;

    public WasapiDeviceEnumerator()
    {
        _enumerator = new MMDeviceEnumerator();
    }

    public event EventHandler? DeviceStateChanged;

    public List<AudioDevice> GetDevices()
    {
        var result = new List<AudioDevice>();

        try
        {
            string defaultId = "";
            try
            {
                using var defaultDevice = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                defaultId = defaultDevice.ID;
            }
            catch { /* no default device */ }

            var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (var device in devices)
            {
                result.Add(new AudioDevice(
                    Id: device.ID,
                    Name: device.FriendlyName,
                    IsDefault: device.ID == defaultId,
                    IsActive: device.State == DeviceState.Active));
            }
        }
        catch
        {
            // Device enumeration can fail if audio service is unavailable
        }

        return result;
    }

    /// <summary>Call this to manually trigger a device state change notification.</summary>
    public void NotifyDeviceStateChanged()
    {
        DeviceStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _enumerator.Dispose();
    }
}
