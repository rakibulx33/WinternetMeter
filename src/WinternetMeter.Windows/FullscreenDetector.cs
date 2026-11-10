using System.Text;
using static WinternetMeter.Windows.NativeMethods;

namespace WinternetMeter.Windows;

/// <summary>
/// "Is something fullscreen on the widget's monitor?"
///
/// Two checks:
/// 1. Windows' own answer (SHQueryUserNotificationState): exclusive games, presentations, fullscreen apps.
/// 2. The foreground window covers its whole monitor: borderless games, F11 browsers, video players.
///    Maximized normal windows (with a title bar) are not fullscreen — this matters with an auto-hide taskbar.
/// </summary>
internal static class FullscreenDetector
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "WorkerW", "Progman", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
    };

    public static bool IsFullscreenOnMonitorOf(IntPtr widget)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == widget
            || foreground == GetShellWindow() || foreground == GetDesktopWindow())
            return false;

        var monitor = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);
        if (monitor != MonitorFromWindow(widget, MONITOR_DEFAULTTONEAREST))
            return false; // fullscreen on another monitor: leave our widget alone

        if (SHQueryUserNotificationState(out int state) == 0
            && state is QUNS_BUSY or QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_PRESENTATION_MODE)
            return true;

        var className = new StringBuilder(64);
        GetClassName(foreground, className, className.Capacity);
        if (ShellClasses.Contains(className.ToString()))
            return false;

        if (IsZoomed(foreground) && (GetWindowLong(foreground, GWL_STYLE) & WS_CAPTION) == WS_CAPTION)
            return false;

        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!GetWindowRect(foreground, out var r) || !GetMonitorInfo(monitor, ref info))
            return false;

        var m = info.rcMonitor;
        return r.Left <= m.Left && r.Top <= m.Top && r.Right >= m.Right && r.Bottom >= m.Bottom;
    }
}
