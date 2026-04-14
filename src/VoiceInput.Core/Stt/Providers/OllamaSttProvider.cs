using System.Threading;
using System.Threading.Tasks;
using VoiceInput.Core.Config;

namespace VoiceInput.Core.Stt.Providers;

/// <summary>
/// Stub STT provider for Ollama. Audio transcription is not yet supported by Ollama.
/// </summary>
public sealed class OllamaSttProvider : ISttProvider
{
    public string Name => "Ollama";
    public SttProviderType ProviderType => SttProviderType.Ollama;
    public bool IsAvailable => false;

    public Task<SttResult> TranscribeAsync(byte[] audio, string language, CancellationToken ct = default)
    {
        throw new SttException(
            SttErrorKind.Unavailable,
            "Ollama does not support audio transcription yet. Track progress: https://github.com/ollama/ollama/pull/15243");
    }
}
