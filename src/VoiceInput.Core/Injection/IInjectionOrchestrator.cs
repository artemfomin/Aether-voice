using System.Threading.Tasks;

namespace VoiceInput.Core.Injection;

/// <summary>
/// Orchestrates the full text injection flow: save clipboard → set text → paste → restore clipboard.
/// </summary>
public interface IInjectionOrchestrator
{
    /// <summary>
    /// Injects text into the currently focused window via clipboard + SendInput.
    /// </summary>
    Task<InjectionResult> InjectAsync(string text);
}
