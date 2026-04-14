using System;
using FluentAssertions;
using VoiceInput.App.Hotkey;
using Xunit;

namespace VoiceInput.Tests.Hotkey;

public class HotkeyTests
{
    [Fact]
    public void GlobalHotkeyService_IsDisposable()
    {
        typeof(GlobalHotkeyService).Should().Implement<IDisposable>();
    }

    [Fact]
    public void GlobalHotkeyService_HasHotkeyPressedEvent()
    {
        typeof(GlobalHotkeyService).GetEvent("HotkeyPressed").Should().NotBeNull();
    }

    [Fact]
    public void GlobalHotkeyService_HasRegisterMethod()
    {
        typeof(GlobalHotkeyService).GetMethod("Register").Should().NotBeNull();
    }

    [Fact]
    public void GlobalHotkeyService_HasUnregisterMethod()
    {
        typeof(GlobalHotkeyService).GetMethod("Unregister").Should().NotBeNull();
    }

    [Fact]
    public void GlobalHotkeyService_DisposeDoesNotThrow()
    {
        var service = new GlobalHotkeyService();
        var act = () => service.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void GlobalHotkeyService_DoubleDisposeDoesNotThrow()
    {
        var service = new GlobalHotkeyService();
        service.Dispose();
        var act = () => service.Dispose();
        act.Should().NotThrow();
    }
}
