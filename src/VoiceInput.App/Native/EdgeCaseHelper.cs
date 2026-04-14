using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VoiceInput.App.Native;

/// <summary>
/// Utility methods for edge case detection: elevated processes, full-screen apps, etc.
/// </summary>
[SupportedOSPlatform("windows")]
public static class EdgeCaseHelper
{
    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int pquns);

    // QUNS values
    private const int QunsRunningD3dFullScreen = 3;
    private const int QunsBusy = 2;

    /// <summary>
    /// Returns true if a full-screen D3D application is running.
    /// </summary>
    public static bool IsFullScreenAppRunning()
    {
        try
        {
            int result = SHQueryUserNotificationState(out int state);
            if (result != 0) return false;
            return state == QunsRunningD3dFullScreen;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns true if the system is in "busy" state (presentation mode, etc.).
    /// </summary>
    public static bool IsSystemBusy()
    {
        try
        {
            int result = SHQueryUserNotificationState(out int state);
            if (result != 0) return false;
            return state == QunsBusy || state == QunsRunningD3dFullScreen;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if running on Windows 11 (build >= 22000).
    /// </summary>
    public static bool IsWindows11OrLater()
    {
        return Environment.OSVersion.Version.Build >= 22000;
    }
}
