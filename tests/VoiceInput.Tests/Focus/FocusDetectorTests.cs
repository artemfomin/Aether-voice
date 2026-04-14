using FluentAssertions;
using VoiceInput.Core.Focus;
using Xunit;

namespace VoiceInput.Tests.Focus;

public class FocusDetectorTests
{
    [Fact]
    public void FocusRect_Properties()
    {
        var rect = new FocusRect(10, 20, 300, 40);
        rect.X.Should().Be(10);
        rect.Y.Should().Be(20);
        rect.Width.Should().Be(300);
        rect.Height.Should().Be(40);
    }

    [Fact]
    public void TextFieldFocusedEventArgs_Properties()
    {
        var args = new TextFieldFocusedEventArgs
        {
            CaretBounds = new FocusRect(0, 0, 100, 20),
            ProcessName = "notepad",
            WindowTitle = "Untitled",
            IsPassword = false,
            IsReadOnly = false
        };

        args.ProcessName.Should().Be("notepad");
        args.WindowTitle.Should().Be("Untitled");
        args.IsPassword.Should().BeFalse();
        args.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void TextFieldFocusedEventArgs_PasswordField()
    {
        var args = new TextFieldFocusedEventArgs
        {
            CaretBounds = new FocusRect(0, 0, 100, 20),
            ProcessName = "chrome",
            WindowTitle = "Login",
            IsPassword = true,
            IsReadOnly = false
        };

        args.IsPassword.Should().BeTrue();
    }

    [Fact]
    public void IFocusDetector_InterfaceContract()
    {
        typeof(IFocusDetector).Should().BeAssignableTo<IDisposable>();
        typeof(IFocusDetector).GetMethod("StartMonitoring").Should().NotBeNull();
        typeof(IFocusDetector).GetMethod("StopMonitoring").Should().NotBeNull();
        typeof(IFocusDetector).GetProperty("IsMonitoring").Should().NotBeNull();
        typeof(IFocusDetector).GetEvent("TextFieldFocused").Should().NotBeNull();
        typeof(IFocusDetector).GetEvent("TextFieldLostFocus").Should().NotBeNull();
    }
}
