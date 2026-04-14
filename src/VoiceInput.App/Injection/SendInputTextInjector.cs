using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using VoiceInput.Core.Injection;
using static VoiceInput.App.Injection.InjectionNativeMethods;

namespace VoiceInput.App.Injection;

/// <summary>
/// Injects text by simulating keyboard shortcuts via the Win32 SendInput API.
/// Detects terminal windows and substitutes Ctrl+Shift+V for Ctrl+V.
/// Silently skips injection when the target process runs at a higher integrity level.
/// </summary>
/// <remarks>
/// The caller is responsible for placing the text in the clipboard before invoking
/// <see cref="InjectTextAsync"/>.  This class only sends the appropriate key sequence;
/// it does not manage clipboard state.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class SendInputTextInjector : ITextInjector
{
    // ── Terminal window class names ───────────────────────────────────────────

    private static readonly HashSet<string> TerminalWindowClasses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "CASCADIA_HOSTING_WINDOW_CLASS", // Windows Terminal (wt.exe)
            "ConsoleWindowClass",             // conhost.exe (cmd.exe / PowerShell)
            "mintty",                         // Git Bash / MSYS2 / Cygwin
            "PuTTY",                          // PuTTY SSH client
            "Alacritty",                      // Alacritty GPU terminal
            "org.wezfurlong.wezterm",         // WezTerm
        };

    private readonly ILogger<SendInputTextInjector> _logger;

    /// <summary>Initialises the injector with a provided logger.</summary>
    public SendInputTextInjector(ILogger<SendInputTextInjector> logger)
    {
        _logger = logger;
    }

    // ── ITextInjector ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<InjectionResult> InjectTextAsync(string text, IntPtr targetWindow)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return Task.Run(() => Inject(targetWindow));
    }

    // ── Strategy resolution (internal for testability) ────────────────────────

    /// <summary>
    /// Maps a window class name to the appropriate injection strategy.
    /// Terminal classes receive <see cref="InjectionStrategy.CtrlShiftV"/>;
    /// all other windows receive <see cref="InjectionStrategy.CtrlV"/>.
    /// </summary>
    internal static InjectionStrategy GetInjectionStrategy(string className) =>
        TerminalWindowClasses.Contains(className)
            ? InjectionStrategy.CtrlShiftV
            : InjectionStrategy.CtrlV;

    // ── Private implementation ────────────────────────────────────────────────

    private InjectionResult Inject(IntPtr targetWindow)
    {
        var hWnd = targetWindow == IntPtr.Zero
            ? GetForegroundWindow()
            : targetWindow;

        if (hWnd == IntPtr.Zero)
        {
            _logger.LogWarning("Text injection aborted: no foreground window found.");
            return InjectionResult.Error;
        }

        if (IsTargetProcessElevated(hWnd))
        {
            _logger.LogInformation(
                "Text injection skipped: target process runs at higher integrity level.");
            return InjectionResult.SkippedElevated;
        }

        var className = GetWindowClassName(hWnd);
        var strategy  = GetInjectionStrategy(className);

        _logger.LogDebug(
            "Injecting via {Strategy} into window class '{Class}'.", strategy, className);

        return strategy == InjectionStrategy.CtrlShiftV
            ? PerformCtrlShiftV()
            : PerformCtrlV();
    }

    private static string GetWindowClassName(IntPtr hWnd)
    {
        var buffer = new System.Text.StringBuilder(ClassNameCapacity);
        return GetClassName(hWnd, buffer, buffer.Capacity) > 0
            ? buffer.ToString()
            : string.Empty;
    }

    // ── Elevation detection ───────────────────────────────────────────────────

    private static bool IsTargetProcessElevated(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint processId);
        if (processId == 0)
            return false;

        var hProcess = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (hProcess == IntPtr.Zero)
            return true; // Access denied — target is likely elevated

        try
        {
            var targetLevel = ReadProcessIntegrityLevel(hProcess);
            var ourLevel    = ReadProcessIntegrityLevel(GetCurrentProcess());
            return targetLevel > ourLevel;
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    private static uint ReadProcessIntegrityLevel(IntPtr hProcess)
    {
        if (!OpenProcessToken(hProcess, TokenQuery, out var hToken))
            return 0;

        try
        {
            return ReadTokenIntegrityLevel(hToken);
        }
        finally
        {
            CloseHandle(hToken);
        }
    }

    private static uint ReadTokenIntegrityLevel(IntPtr hToken)
    {
        // First call: discover the required buffer size.
        GetTokenInformation(
            hToken,
            TokenInformationClass.TokenIntegrityLevel,
            IntPtr.Zero, 0,
            out uint size);

        if (size == 0)
            return 0;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (!GetTokenInformation(
                    hToken,
                    TokenInformationClass.TokenIntegrityLevel,
                    buffer, size,
                    out _))
                return 0;

            var label     = Marshal.PtrToStructure<TOKEN_MANDATORY_LABEL>(buffer);
            var countPtr  = GetSidSubAuthorityCount(label.Label.Sid);
            if (countPtr == IntPtr.Zero)
                return 0;

            var count  = Marshal.ReadByte(countPtr);
            if (count == 0)
                return 0;

            var ridPtr = GetSidSubAuthority(label.Label.Sid, (uint)(count - 1));
            return ridPtr == IntPtr.Zero ? 0 : (uint)Marshal.ReadInt32(ridPtr);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // ── SendInput helpers ─────────────────────────────────────────────────────

    private static InjectionResult PerformCtrlV()
    {
        var inputs = new[]
        {
            MakeKeyDown(VkControl),
            MakeKeyDown(VkV),
            MakeKeyUp(VkV),
            MakeKeyUp(VkControl),
        };
        return SendAll(inputs);
    }

    private static InjectionResult PerformCtrlShiftV()
    {
        var inputs = new[]
        {
            MakeKeyDown(VkControl),
            MakeKeyDown(VkShift),
            MakeKeyDown(VkV),
            MakeKeyUp(VkV),
            MakeKeyUp(VkShift),
            MakeKeyUp(VkControl),
        };
        return SendAll(inputs);
    }

    private static InjectionResult SendAll(INPUT[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        return sent == (uint)inputs.Length
            ? InjectionResult.Success
            : InjectionResult.Error;
    }

    private static INPUT MakeKeyDown(ushort vk) => MakeKeyInput(vk, 0);

    private static INPUT MakeKeyUp(ushort vk) => MakeKeyInput(vk, KeyeventfKeyup);

    private static INPUT MakeKeyInput(ushort vk, uint flags) => new()
    {
        Type = InputKeyboard,
        U    = new InputUnion { Ki = new KEYBDINPUT { VirtualKeyCode = vk, Flags = flags } },
    };
}
