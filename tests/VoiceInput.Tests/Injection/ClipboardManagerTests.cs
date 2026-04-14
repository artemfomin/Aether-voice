using System.Runtime.ExceptionServices;
using FluentAssertions;
using VoiceInput.App.Injection;
using VoiceInput.Core.Injection;
using Xunit;

namespace VoiceInput.Tests.Injection;

/// <summary>
/// Tests for <see cref="IClipboardManager"/> interface contract and
/// <see cref="Win32ClipboardManager"/> behaviour.
/// </summary>
/// <remarks>
/// Integration tests that touch the real system clipboard are wrapped in
/// <see cref="RunOnStaThread"/> to be explicit about thread model, though
/// Win32 clipboard P/Invoke does not strictly require an STA apartment.
/// Each integration test uses unique sentinel strings to avoid conflicts
/// when tests run concurrently.
/// </remarks>
public sealed class ClipboardManagerTests
{
    // ── IClipboardManager interface contract ─────────────────────────────────

    [Fact]
    public void IClipboardManager_HasSaveState_Method()
    {
        var method = typeof(IClipboardManager)
            .GetMethod(nameof(IClipboardManager.SaveState));

        method.Should().NotBeNull(
            "IClipboardManager must declare a public SaveState() method");
        method!.ReturnType.Should().Be(typeof(void),
            "SaveState must return void");
        method.GetParameters().Should().BeEmpty(
            "SaveState takes no parameters");
    }

    [Fact]
    public void IClipboardManager_HasRestoreState_Method()
    {
        var method = typeof(IClipboardManager)
            .GetMethod(nameof(IClipboardManager.RestoreState));

        method.Should().NotBeNull(
            "IClipboardManager must declare a public RestoreState() method");
        method!.ReturnType.Should().Be(typeof(void),
            "RestoreState must return void");
        method.GetParameters().Should().BeEmpty(
            "RestoreState takes no parameters");
    }

    [Fact]
    public void IClipboardManager_HasSetText_Method()
    {
        var method = typeof(IClipboardManager)
            .GetMethod(nameof(IClipboardManager.SetText));

        method.Should().NotBeNull(
            "IClipboardManager must declare a public SetText(string) method");
        method!.ReturnType.Should().Be(typeof(void),
            "SetText must return void");

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1,
            "SetText takes exactly one parameter");
        parameters[0].ParameterType.Should().Be(typeof(string),
            "the single SetText parameter must be of type string");
    }

    [Fact]
    public void IClipboardManager_HasGetText_Method()
    {
        var method = typeof(IClipboardManager)
            .GetMethod(nameof(IClipboardManager.GetText));

        method.Should().NotBeNull(
            "IClipboardManager must declare a public GetText() method");
        // string? at runtime is typeof(string) — nullability is compile-time only.
        method!.ReturnType.Should().Be(typeof(string),
            "GetText must return string (nullable string? is typeof(string) at runtime)");
        method.GetParameters().Should().BeEmpty(
            "GetText takes no parameters");
    }

    [Fact]
    public void Win32ClipboardManager_Implements_IClipboardManager()
    {
        typeof(Win32ClipboardManager)
            .GetInterfaces()
            .Should().Contain(typeof(IClipboardManager),
                "Win32ClipboardManager must implement IClipboardManager");
    }

    // ── Win32ClipboardManager integration ───────────────────────────────────

    [Fact]
    public void SetText_Then_GetText_RoundTrip_ReturnsExpectedText()
    {
        RunOnStaThread(() =>
        {
            var manager = new Win32ClipboardManager();

            manager.SetText("VoiceInput_RoundTrip_Test_A1B2");

            manager.GetText().Should().Be("VoiceInput_RoundTrip_Test_A1B2",
                "GetText must return the exact value placed by SetText");
        });
    }

    [Fact]
    public void SaveState_SetText_RestoreState_ReturnsOriginalText()
    {
        RunOnStaThread(() =>
        {
            var manager = new Win32ClipboardManager();
            manager.SetText("VoiceInput_Original_C3D4");

            manager.SaveState();
            manager.SetText("VoiceInput_Temporary_E5F6");
            manager.RestoreState();

            manager.GetText().Should().Be("VoiceInput_Original_C3D4",
                "RestoreState must bring back the text saved by SaveState");
        });
    }

    [Fact]
    public void RestoreState_InFinallyBlock_AfterException_StillRestores()
    {
        // Verifies the caller's try/finally pattern: even if injection fails,
        // the clipboard is always restored via RestoreState in the finally block.
        RunOnStaThread(() =>
        {
            var manager = new Win32ClipboardManager();
            manager.SetText("VoiceInput_SafetyNet_G7H8");
            manager.SaveState();

            try
            {
                manager.SetText("VoiceInput_Injected_I9J0");
                throw new InvalidOperationException("Simulated injection failure");
            }
            catch (InvalidOperationException)
            {
                // Expected — swallowed deliberately in this test.
            }
            finally
            {
                // This is the pattern callers MUST use.
                manager.RestoreState();
            }

            manager.GetText().Should().Be("VoiceInput_SafetyNet_G7H8",
                "clipboard must be restored in the finally block even when an exception occurs");
        });
    }

    [Fact]
    public void SaveState_DoesNotThrow()
    {
        RunOnStaThread(() =>
        {
            var manager = new Win32ClipboardManager();
            Action act = manager.SaveState;

            act.Should().NotThrow(
                "SaveState must handle any clipboard state gracefully without throwing");
        });
    }

    [Fact]
    public void RestoreState_WithNoPriorSave_DoesNotThrow()
    {
        var manager = new Win32ClipboardManager();
        Action act = manager.RestoreState;

        // RestoreState with empty _savedState is a no-op — no clipboard access.
        act.Should().NotThrow(
            "RestoreState with no prior SaveState must be a safe no-op");
    }

    [Fact]
    public void GetText_DoesNotThrow_RegardlessOfClipboardState()
    {
        RunOnStaThread(() =>
        {
            var manager = new Win32ClipboardManager();
            Action act = () => _ = manager.GetText();

            act.Should().NotThrow(
                "GetText must not throw regardless of what is currently on the clipboard");
        });
    }

    [Fact]
    public void SetText_WithEmptyString_DoesNotThrow()
    {
        RunOnStaThread(() =>
        {
            var manager = new Win32ClipboardManager();
            Action act = () => manager.SetText(string.Empty);

            act.Should().NotThrow(
                "SetText with an empty string must be handled gracefully");
        });
    }

    // ── STA thread helper ─────────────────────────────────────────────────────

    /// <summary>
    /// Runs <paramref name="action"/> on a new STA thread and re-throws any
    /// exception on the calling thread, preserving the original stack trace.
    /// </summary>
    /// <remarks>
    /// Win32 clipboard APIs do not strictly require STA, but running integration
    /// tests on an explicit STA thread is consistent with how clipboard code
    /// runs in production (WPF UI thread is always STA).
    /// </remarks>
    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(timeout: TimeSpan.FromSeconds(5));

        captured?.Throw();
    }
}
