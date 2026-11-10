using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using RayLite.Native;
using RayLite.Services;
using RayLite.ViewModels;

namespace RayLite;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.HideRequested += HideLauncher;
        Deactivated += (_, _) => HideLauncher();
    }

    /// <summary>Renders the window once (invisible) so the first hotkey press is instant.</summary>
    public void Prewarm()
    {
        Opacity = 0;
        ShowActivated = false;
        Show();
        Hide();
        ShowActivated = true;
        Opacity = 1;
    }

    public void Toggle()
    {
        if (IsVisible) HideLauncher();
        else ShowLauncher();
    }

    public void ShowLauncher()
    {
        _viewModel.Reset();
        PositionOnActiveMonitor();

        if (!IsVisible) Show();
        Activate();
        ForceForeground();

        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    public void HideLauncher()
    {
        if (!IsVisible) return;

        Hide();
        _viewModel.Reset();
        MemoryTrimmer.ScheduleTrim();
    }

    private void ResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        var container = ItemsControl.ContainerFromElement((ItemsControl)sender, source) as ListBoxItem;
        if (container?.DataContext is ResultItem item)
        {
            _viewModel.Open(item);
            e.Handled = true;
        }
    }

    /// <summary>Centers horizontally and places the bar at 20% of the work area height on the monitor under the cursor.</summary>
    private void PositionOnActiveMonitor()
    {
        IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();

        NativeMethods.GetCursorPos(out var cursor);
        IntPtr monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);

        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return;

        double scale = 1.0;
        if (NativeMethods.GetDpiForMonitor(monitor, 0, out uint dpiX, out _) == 0 && dpiX > 0)
            scale = dpiX / 96.0;

        int widthPx = (int)Math.Round(Width * scale);
        int workWidth = info.rcWork.Right - info.rcWork.Left;
        int workHeight = info.rcWork.Bottom - info.rcWork.Top;

        int x = info.rcWork.Left + (workWidth - widthPx) / 2;
        int y = info.rcWork.Top + (int)(workHeight * 0.20);

        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_TOPMOST, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>Windows restricts SetForegroundWindow; attaching to the foreground thread's input queue works around it.</summary>
    private void ForceForeground()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (foreground == hwnd) return;

        uint foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        uint currentThread = NativeMethods.GetCurrentThreadId();

        bool attached = foregroundThread != 0 && foregroundThread != currentThread &&
                        NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);

        NativeMethods.SetForegroundWindow(hwnd);

        if (attached) NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
    }
}
