using FluentAssertions;
using VoiceInput.App.Injection;
using VoiceInput.Core.Injection;
using Xunit;

namespace VoiceInput.Tests.Injection;

/// <summary>
/// Tests for <see cref="SendInputTextInjector.GetInjectionStrategy"/>.
/// Verifies that terminal window classes are mapped to <see cref="InjectionStrategy.CtrlShiftV"/>
/// and all other classes (including unknown and empty) fall back to <see cref="InjectionStrategy.CtrlV"/>.
/// </summary>
public sealed class TextInjectorTests
{
    // ── Explicitly required cases (per acceptance criteria) ───────────────────

    [Fact]
    public void GetInjectionStrategy_CascadiaHostingWindowClass_ReturnsCtrlShiftV()
    {
        var strategy = SendInputTextInjector.GetInjectionStrategy("CASCADIA_HOSTING_WINDOW_CLASS");

        strategy.Should().Be(InjectionStrategy.CtrlShiftV,
            "Windows Terminal uses CASCADIA_HOSTING_WINDOW_CLASS and requires Ctrl+Shift+V");
    }

    [Fact]
    public void GetInjectionStrategy_ConsoleWindowClass_ReturnsCtrlShiftV()
    {
        var strategy = SendInputTextInjector.GetInjectionStrategy("ConsoleWindowClass");

        strategy.Should().Be(InjectionStrategy.CtrlShiftV,
            "cmd.exe / conhost uses ConsoleWindowClass and requires Ctrl+Shift+V");
    }

    [Fact]
    public void GetInjectionStrategy_Notepad_ReturnsCtrlV()
    {
        var strategy = SendInputTextInjector.GetInjectionStrategy("Notepad");

        strategy.Should().Be(InjectionStrategy.CtrlV,
            "Notepad is a normal GUI application and should receive Ctrl+V");
    }

    [Fact]
    public void GetInjectionStrategy_UnknownClass_ReturnsCtrlV()
    {
        var strategy = SendInputTextInjector.GetInjectionStrategy("SomeUnknownWindowClass");

        strategy.Should().Be(InjectionStrategy.CtrlV,
            "unrecognised window classes should default to Ctrl+V");
    }

    [Fact]
    public void GetInjectionStrategy_EmptyClass_ReturnsCtrlV()
    {
        var strategy = SendInputTextInjector.GetInjectionStrategy(string.Empty);

        strategy.Should().Be(InjectionStrategy.CtrlV,
            "an empty class name is not a terminal and should default to Ctrl+V");
    }

    // ── All known terminal classes → CtrlShiftV ───────────────────────────────

    [Theory]
    [InlineData("CASCADIA_HOSTING_WINDOW_CLASS", "Windows Terminal")]
    [InlineData("ConsoleWindowClass",             "cmd.exe / conhost")]
    [InlineData("mintty",                          "Git Bash / MSYS2")]
    [InlineData("PuTTY",                           "PuTTY SSH client")]
    [InlineData("Alacritty",                       "Alacritty GPU terminal")]
    [InlineData("org.wezfurlong.wezterm",          "WezTerm")]
    public void GetInjectionStrategy_AllKnownTerminalClasses_ReturnCtrlShiftV(
        string className, string description)
    {
        var strategy = SendInputTextInjector.GetInjectionStrategy(className);

        strategy.Should().Be(InjectionStrategy.CtrlShiftV,
            $"{description} (class='{className}') is a terminal and requires Ctrl+Shift+V");
    }

    // ── Case-insensitive matching ─────────────────────────────────────────────

    [Theory]
    [InlineData("cascadia_hosting_window_class")]
    [InlineData("CONSOLEWINDOWCLASS")]
    [InlineData("MINTTY")]
    [InlineData("putty")]
    [InlineData("ALACRITTY")]
    [InlineData("ORG.WEZFURLONG.WEZTERM")]
    public void GetInjectionStrategy_TerminalClassDifferentCase_ReturnsCtrlShiftV(string className)
    {
        var strategy = SendInputTextInjector.GetInjectionStrategy(className);

        strategy.Should().Be(InjectionStrategy.CtrlShiftV,
            "terminal class matching must be case-insensitive");
    }

    // ── Non-terminal GUI applications → CtrlV ────────────────────────────────

    [Theory]
    [InlineData("Notepad")]
    [InlineData("Chrome_WidgetWin_1")]
    [InlineData("HwndWrapper[DefaultDomain;;")]
    [InlineData("")]
    public void GetInjectionStrategy_NonTerminalClasses_ReturnCtrlV(string className)
    {
        var strategy = SendInputTextInjector.GetInjectionStrategy(className);

        strategy.Should().Be(InjectionStrategy.CtrlV,
            $"window class '{className}' is not a terminal and must default to Ctrl+V");
    }
}
