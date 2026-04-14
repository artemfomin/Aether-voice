using FluentAssertions;
using VoiceInput.App.Overlay;
using VoiceInput.App.Overlay.Animations;
using VoiceInput.Core.Focus;
using Xunit;

namespace VoiceInput.Tests.Overlay;

public class OverlayTests
{
    [Fact]
    public void IRecordingAnimation_InterfaceContract()
    {
        typeof(IRecordingAnimation).GetMethod("Start").Should().NotBeNull();
        typeof(IRecordingAnimation).GetMethod("Stop").Should().NotBeNull();
        typeof(IRecordingAnimation).GetMethod("UpdateAmplitude").Should().NotBeNull();
        typeof(IRecordingAnimation).GetProperty("Visual").Should().NotBeNull();
    }

    [Fact]
    public void Positioner_PrefersBelowTextField()
    {
        var caret = new FocusRect(100, 200, 300, 20);
        var pos = OverlayPositioner.Calculate(caret, 220, 64);

        pos.Y.Should().BeGreaterThan(200, "should be below the text field");
    }

    [Fact]
    public void Positioner_FlipsAboveWhenNearBottom()
    {
        // Caret near bottom of screen
        double screenHeight = System.Windows.SystemParameters.VirtualScreenHeight;
        var caret = new FocusRect(100, screenHeight - 30, 300, 20);
        var pos = OverlayPositioner.Calculate(caret, 220, 64);

        pos.Y.Should().BeLessThan(screenHeight - 30, "should flip to above");
    }

    [Fact]
    public void Positioner_ClampsHorizontal()
    {
        double screenWidth = System.Windows.SystemParameters.VirtualScreenWidth;
        var caret = new FocusRect(screenWidth - 50, 200, 300, 20);
        var pos = OverlayPositioner.Calculate(caret, 220, 64);

        (pos.X + 220).Should().BeLessOrEqualTo(screenWidth, "should not go off-screen right");
    }

    [Fact]
    public void Positioner_NeverNegative()
    {
        var caret = new FocusRect(-100, -100, 300, 20);
        var pos = OverlayPositioner.Calculate(caret, 220, 64);

        pos.X.Should().BeGreaterOrEqualTo(0);
        pos.Y.Should().BeGreaterOrEqualTo(0);
    }
}
