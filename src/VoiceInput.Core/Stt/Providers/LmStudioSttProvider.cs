using System.Threading;
using System.Threading.Tasks;
using VoiceInput.Core.Config;

namespace VoiceInput.Core.Stt.Providers;

/// <summary>
/// Stub STT provider for LM Studio. Audio transcription is not yet supported.
/// </summary>
public sealed class LmStudioSttProvider : ISttProvider
{
    public string Name => "LM Studio";
    public SttProviderType ProviderType => SttProviderType.LMStudio;
    public bool IsAvailable => false;

    public Task<SttResult> TranscribeAsync(byte[] audio, string language, CancellationToken ct = default)
    {
        throw new SttException(
            SttErrorKind.Unavailable,
            "LM Studio does not support audio transcription yet. Track progress: https://lmstudio.ai/transcribe");
    }
}
