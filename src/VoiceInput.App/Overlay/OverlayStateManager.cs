using System;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Media;
using VoiceInput.App.Overlay.Animations;
using VoiceInput.App.Pipeline;

namespace VoiceInput.App.Overlay;

/// <summary>
/// Manages overlay visual states: idle, recording, processing, error, success.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class OverlayStateManager
{
    private readonly IslandWindow _island;
    private System.Windows.Threading.DispatcherTimer? _autoHideTimer;

    public OverlayStateManager(IslandWindow island)
    {
        _island = island;
    }

    public void ShowRecording(IRecordingAnimation animation)
    {
        CancelAutoHide();
        _island.SetAnimation(animation);
        animation.Start();
        _island.StatusText = "Listening...";
        _island.ShowAnimated();
    }

    public void ShowProcessing()
    {
        _island.CurrentAnimation?.Stop();
        _island.StatusText = "Processing...";
    }

    public void ShowSuccess(string text)
    {
        _island.CurrentAnimation?.Stop();
        _island.StatusText = text.Length > 40 ? text[..37] + "..." : text;
        AutoHideAfter(TimeSpan.FromSeconds(1.5));
    }

    public void ShowError(string message)
    {
        _island.CurrentAnimation?.Stop();
        _island.StatusText = message;
        AutoHideAfter(TimeSpan.FromSeconds(3));
    }

    public void Hide()
    {
        CancelAutoHide();
        _island.CurrentAnimation?.Stop();
        _island.HideAnimated();
    }

    public void UpdateAmplitude(float amplitude)
    {
        _island.CurrentAnimation?.UpdateAmplitude(amplitude);
    }

    public void OnPipelineStateChanged(PipelineState state)
    {
        switch (state)
        {
            case PipelineState.Processing:
                ShowProcessing();
                break;
            case PipelineState.Idle:
                // Handled by ShowSuccess or ShowError externally
                break;
        }
    }

    private void AutoHideAfter(TimeSpan delay)
    {
        CancelAutoHide();
        _autoHideTimer = new System.Windows.Threading.DispatcherTimer { Interval = delay };
        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer.Stop();
            Hide();
        };
        _autoHideTimer.Start();
    }

    private void CancelAutoHide()
    {
        _autoHideTimer?.Stop();
        _autoHideTimer = null;
    }
}
