using System.Threading;
using System.Threading.Tasks;
using VoiceInput.Core.Config;

namespace VoiceInput.Core.Llm;

/// <summary>
/// Post-processes STT text through an LLM (grammar fix, filler removal).
/// </summary>
public interface ILlmProcessor
{
    /// <summary>
    /// Processes raw STT text. Returns polished text, or raw text on failure (graceful degradation).
    /// </summary>
    Task<string> ProcessAsync(string rawText, LlmPostProcessingMode mode, CancellationToken ct = default);
}
