using System.Windows;
using VoiceInput.Core.Focus;

namespace VoiceInput.App.Overlay;

/// <summary>
/// Positions the floating island near the focused text field.
/// Prefers below, flips to above if near screen edge.
/// </summary>
public static class OverlayPositioner
{
    private const double Offset = 8;

    /// <summary>
    /// Calculates the position for the overlay window relative to the caret bounds.
    /// </summary>
    public static Point Calculate(FocusRect caretBounds, double overlayWidth, double overlayHeight)
    {
        double screenWidth = SystemParameters.VirtualScreenWidth;
        double screenHeight = SystemParameters.VirtualScreenHeight;

        // Prefer below the text field
        double x = caretBounds.X;
        double y = caretBounds.Y + caretBounds.Height + Offset;

        // If goes off bottom → place above
        if (y + overlayHeight > screenHeight)
        {
            y = caretBounds.Y - overlayHeight - Offset;
        }

        // Clamp horizontal
        if (x + overlayWidth > screenWidth)
        {
            x = screenWidth - overlayWidth;
        }

        if (x < 0) x = 0;
        if (y < 0) y = 0;

        return new Point(x, y);
    }
}
