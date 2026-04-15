using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using VoiceInput.Core.Injection;

namespace VoiceInput.App.Injection;

/// <summary>
/// Orchestrates text injection: save clipboard → set text → paste → wait → restore.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SmartInjectionOrchestrator : IInjectionOrchestrator
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private readonly IClipboardManager _clipboard;
    private readonly ITextInjector _injector;
    private readonly int _pasteDelayMs;

    public SmartInjectionOrchestrator(
        IClipboardManager clipboard,
        ITextInjector injector,
        int pasteDelayMs = 500)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(injector);
        _clipboard = clipboard;
        _injector = injector;
        _pasteDelayMs = pasteDelayMs;
    }

    public async Task<InjectionResult> InjectAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return InjectionResult.Error;
        }

        var targetWindow = GetForegroundWindow();
        if (targetWindow == IntPtr.Zero)
        {
            return InjectionResult.Error;
        }

        try
        {
            _clipboard.SaveState();

            try
            {
                _clipboard.SetText(text);
                // Small delay to let clipboard settle before sending Ctrl+V
                await Task.Delay(50);
                var result = await _injector.InjectTextAsync(text, targetWindow);

                if (result == InjectionResult.Success)
                {
                    // Wait for paste to be processed before restoring clipboard
                    await Task.Delay(_pasteDelayMs);
                }

                return result;
            }
            finally
            {
                _clipboard.RestoreState();
            }
        }
        catch
        {
            return InjectionResult.Error;
        }
    }
}
