using System.Text;
using RayLite.Native;

namespace RayLite.Services;

/// <summary>
/// Stateless, on-demand fullscreen detection (pure Win32). Called only when the hotkey is pressed.
/// </summary>
internal static class FullscreenDetector
{
    // QUERY_USER_NOTIFICATION_STATE values
    private const int QUNS_BUSY = 2;
    private const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
    private const int QUNS_PRESENTATION_MODE = 4;

    public static bool IsFullscreenAppActive()
    {
        return IsNotificationStateFullscreen() || IsForegroundWindowCoveringMonitor();
    }

    /// <summary>Shell's own view: exclusive-mode D3D games, fullscreen "busy" apps, presentation mode.</summary>
    private static bool IsNotificationStateFullscreen()
    {
        try
        {
            if (NativeMethods.SHQueryUserNotificationState(out int state) != 0) return false;
            return state is QUNS_BUSY or QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_PRESENTATION_MODE;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Fallback for borderless-fullscreen games and fullscreen video: the foreground window covers its whole
    /// monitor and has no title bar. The desktop, shell/taskbar windows and our own process are excluded.
    /// </summary>
    private static bool IsForegroundWindowCoveringMonitor()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero ||
            foreground == NativeMethods.GetDesktopWindow() ||
            foreground == NativeMethods.GetShellWindow())
            return false;

        NativeMethods.GetWindowThreadProcessId(foreground, out uint processId);
        if (processId == (uint)Environment.ProcessId) return false;

        var className = new StringBuilder(64);
        if (NativeMethods.GetClassName(foreground, className, className.Capacity) > 0)
        {
            string name = className.ToString();
            if (name is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        }

        if (!NativeMethods.GetWindowRect(foreground, out var window)) return false;

        IntPtr monitor = NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return false;

        bool coversMonitor =
            window.Left <= info.rcMonitor.Left && window.Top <= info.rcMonitor.Top &&
            window.Right >= info.rcMonitor.Right && window.Bottom >= info.rcMonitor.Bottom;
        if (!coversMonitor) return false;

        // A maximized normal window also fills the work area; real fullscreen windows have no title bar.
        uint style = (uint)NativeMethods.GetWindowLong(foreground, NativeMethods.GWL_STYLE);
        return (style & NativeMethods.WS_CAPTION) != NativeMethods.WS_CAPTION;
    }
}
