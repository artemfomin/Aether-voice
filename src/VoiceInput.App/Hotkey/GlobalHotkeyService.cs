using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Input;
using System.Windows.Interop;

namespace VoiceInput.App.Hotkey;

/// <summary>
/// Registers system-wide hotkeys via Win32 RegisterHotKey/UnregisterHotKey.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x1001;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _hwndSource;
    private bool _registered;
    private bool _disposed;

    /// <summary>Fires when the registered hotkey is pressed.</summary>
    public event EventHandler? HotkeyPressed;

    /// <summary>
    /// Registers a global hotkey. Returns false if the combination is already taken.
    /// </summary>
    public bool Register(ModifierKeys modifiers, Key key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Unregister();

        // Create hidden message-only window
        _hwndSource = new HwndSource(new HwndSourceParameters("VoiceInputHotkey")
        {
            Width = 0,
            Height = 0,
            PositionX = -100,
            PositionY = -100,
            WindowStyle = 0
        });
        _hwndSource.AddHook(WndProc);

        uint fsModifiers = 0;
        if (modifiers.HasFlag(ModifierKeys.Control)) fsModifiers |= 0x0002;
        if (modifiers.HasFlag(ModifierKeys.Shift)) fsModifiers |= 0x0004;
        if (modifiers.HasFlag(ModifierKeys.Alt)) fsModifiers |= 0x0001;
        if (modifiers.HasFlag(ModifierKeys.Windows)) fsModifiers |= 0x0008;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        _registered = RegisterHotKey(_hwndSource.Handle, HotkeyId, fsModifiers, vk);
        return _registered;
    }

    /// <summary>
    /// Unregisters the current hotkey.
    /// </summary>
    public void Unregister()
    {
        if (_registered && _hwndSource != null)
        {
            UnregisterHotKey(_hwndSource.Handle, HotkeyId);
            _registered = false;
        }

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
            _hwndSource = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unregister();
    }
}
