namespace VoiceInput.Core.Audio;

/// <summary>
/// Represents an audio input device.
/// </summary>
public sealed record AudioDevice(string Id, string Name, bool IsDefault, bool IsActive);
