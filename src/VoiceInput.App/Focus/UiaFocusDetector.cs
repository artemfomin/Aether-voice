using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Automation;
using VoiceInput.Core.Focus;

namespace VoiceInput.App.Focus;

/// <summary>
/// Detects text input field focus using Microsoft UI Automation.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UiaFocusDetector : IFocusDetector
{
    private readonly object _lock = new();
    private Timer? _debounceTimer;
    private AutomationElement? _pendingElement;
    private bool _isMonitoring;
    private bool _wasInTextField;
    private bool _disposed;

    private const int DebounceMs = 100;

    public bool IsMonitoring => _isMonitoring;

    public event EventHandler<TextFieldFocusedEventArgs>? TextFieldFocused;
    public event EventHandler? TextFieldLostFocus;

    public void StartMonitoring()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isMonitoring) return;

        Automation.AddAutomationFocusChangedEventHandler(OnFocusChanged);
        _isMonitoring = true;
    }

    public void StopMonitoring()
    {
        if (!_isMonitoring) return;

        Automation.RemoveAutomationFocusChangedEventHandler(OnFocusChanged);
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _isMonitoring = false;
    }

    private void OnFocusChanged(object sender, AutomationFocusChangedEventArgs e)
    {
        if (!_isMonitoring) return;

        try
        {
            var element = sender as AutomationElement;
            if (element == null) return;

            lock (_lock)
            {
                _pendingElement = element;
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(OnDebounceElapsed, null, DebounceMs, Timeout.Infinite);
            }
        }
        catch
        {
            // UI Automation can throw if element is gone
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        AutomationElement? element;
        lock (_lock)
        {
            element = _pendingElement;
            _pendingElement = null;
        }

        if (element == null) return;

        try
        {
            var controlType = element.Current.ControlType;
            bool isTextField = controlType == ControlType.Edit || controlType == ControlType.Document;

            if (isTextField)
            {
                bool isPassword = false;
                bool isReadOnly = false;

                try { isPassword = element.Current.IsPassword; } catch { /* some elements don't support this */ }

                try
                {
                    if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) && pattern is ValuePattern vp)
                    {
                        isReadOnly = vp.Current.IsReadOnly;
                    }
                }
                catch { /* optional */ }

                // Filter out password and readonly fields
                if (isPassword || isReadOnly)
                {
                    if (_wasInTextField)
                    {
                        _wasInTextField = false;
                        TextFieldLostFocus?.Invoke(this, EventArgs.Empty);
                    }
                    return;
                }

                var rect = element.Current.BoundingRectangle;
                string processName = "";
                string windowTitle = "";

                try
                {
                    int pid = element.Current.ProcessId;
                    using var process = Process.GetProcessById(pid);
                    processName = process.ProcessName;
                }
                catch { /* process may have exited */ }

                try { windowTitle = element.Current.Name ?? ""; } catch { /* optional */ }

                _wasInTextField = true;
                TextFieldFocused?.Invoke(this, new TextFieldFocusedEventArgs
                {
                    CaretBounds = new FocusRect(rect.X, rect.Y, rect.Width, rect.Height),
                    ProcessName = processName,
                    WindowTitle = windowTitle,
                    IsPassword = isPassword,
                    IsReadOnly = isReadOnly
                });
            }
            else
            {
                if (_wasInTextField)
                {
                    _wasInTextField = false;
                    TextFieldLostFocus?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch
        {
            // Element may have been destroyed between debounce and processing
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopMonitoring();
    }
}
