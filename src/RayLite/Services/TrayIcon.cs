using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using RayLite.Native;

namespace RayLite.Services;

/// <summary>Native Shell_NotifyIcon tray icon (no WinForms dependency, to keep the footprint small).</summary>
internal sealed class TrayIcon : IDisposable
{
    private const uint WM_TRAY = 0x8001; // WM_APP + 1
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint NIM_ADD = 0, NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;

    private const uint MF_STRING = 0x0000, MF_SEPARATOR = 0x0800;
    private const uint TPM_RIGHTBUTTON = 0x0002, TPM_BOTTOMALIGN = 0x0020, TPM_NONOTIFY = 0x0080, TPM_RETURNCMD = 0x0100;

    private const uint CmdOpen = 1, CmdSettings = 2, CmdRebuild = 3, CmdExit = 4;

    private readonly HotKeyManager _host;
    private readonly HwndSourceHook _hook;
    private readonly uint _taskbarCreated;
    private readonly IntPtr _icon;
    private bool _added;

    public event Action? OpenRequested;
    public event Action? SettingsRequested;
    public event Action? RebuildRequested;
    public event Action? ExitRequested;

    public TrayIcon(HotKeyManager host)
    {
        _host = host;
        _hook = WndProc;
        _taskbarCreated = NativeMethods.RegisterWindowMessageW("TaskbarCreated");
        _icon = LoadTrayIcon();
        _host.AddHook(_hook);
        Add();
    }

    /// <summary>
    /// Loads the embedded app.ico (best-matching size for the tray). If the resource is missing or unreadable,
    /// falls back to the stock Windows application icon (the same icon System.Drawing.SystemIcons.Application wraps,
    /// loaded natively so System.Drawing never has to be loaded).
    /// </summary>
    private static IntPtr LoadTrayIcon()
    {
        IntPtr icon = LoadEmbeddedIcon();
        return icon != IntPtr.Zero
            ? icon
            : NativeMethods.LoadIconW(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION
    }

    private static IntPtr LoadEmbeddedIcon()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
            if (resource == null) return IntPtr.Zero;

            byte[] data;
            using (Stream stream = resource.Stream)
            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                data = buffer.ToArray();
            }

            // ICONDIR: reserved(2) type(2) count(2); ICONDIRENTRY (16 bytes each)
            if (data.Length < 6 || BitConverter.ToUInt16(data, 2) != 1) return IntPtr.Zero;
            int count = BitConverter.ToUInt16(data, 4);

            int desired = Math.Max(16, NativeMethods.GetSystemMetrics(49)); // SM_CXSMICON
            int bestOffset = -1, bestSize = 0, bestScore = int.MaxValue;

            for (int i = 0; i < count; i++)
            {
                int entry = 6 + i * 16;
                if (entry + 16 > data.Length) break;

                int width = data[entry] == 0 ? 256 : data[entry];
                int size = (int)BitConverter.ToUInt32(data, entry + 8);
                int offset = (int)BitConverter.ToUInt32(data, entry + 12);
                if (offset < 0 || size <= 0 || offset + size > data.Length) continue;

                int score = width >= desired ? width - desired : (desired - width) + 1000;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestOffset = offset;
                    bestSize = size;
                }
            }

            if (bestOffset < 0) return IntPtr.Zero;

            var image = new byte[bestSize];
            Array.Copy(data, bestOffset, image, 0, bestSize);
            return NativeMethods.CreateIconFromResourceEx(image, (uint)bestSize, true, 0x00030000, desired, desired, 0);
        }
        catch (Exception)
        {
            return IntPtr.Zero; // resource missing/corrupt -> caller falls back to the stock icon
        }
    }

    private NativeMethods.NOTIFYICONDATA BuildData() => new()
    {
        cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
        hWnd = _host.Handle,
        uID = 1,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
        uCallbackMessage = WM_TRAY,
        hIcon = _icon,
        szTip = "RayLite",
        szInfo = string.Empty,
        szInfoTitle = string.Empty
    };

    private void Add()
    {
        var data = BuildData();
        _added = NativeMethods.Shell_NotifyIconW(NIM_ADD, ref data);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAY)
        {
            handled = true;
            int mouseMessage = (int)(lParam.ToInt64() & 0xFFFF);
            if (mouseMessage == WM_LBUTTONUP) OpenRequested?.Invoke();
            else if (mouseMessage == WM_RBUTTONUP) ShowMenu();
        }
        else if (_taskbarCreated != 0 && (uint)msg == _taskbarCreated)
        {
            Add(); // Explorer restarted
        }
        return IntPtr.Zero;
    }

    private void ShowMenu()
    {
        NativeMethods.GetCursorPos(out var pt);
        IntPtr menu = NativeMethods.CreatePopupMenu();
        try
        {
            NativeMethods.AppendMenuW(menu, MF_STRING, new UIntPtr(CmdOpen), "Open RayLite");
            NativeMethods.AppendMenuW(menu, MF_STRING, new UIntPtr(CmdSettings), "Settings…");
            NativeMethods.AppendMenuW(menu, MF_STRING, new UIntPtr(CmdRebuild), "Rebuild index");
            NativeMethods.AppendMenuW(menu, MF_SEPARATOR, UIntPtr.Zero, null);
            NativeMethods.AppendMenuW(menu, MF_STRING, new UIntPtr(CmdExit), "Exit");

            NativeMethods.SetForegroundWindow(_host.Handle);
            int cmd = NativeMethods.TrackPopupMenu(
                menu, TPM_RIGHTBUTTON | TPM_BOTTOMALIGN | TPM_NONOTIFY | TPM_RETURNCMD,
                pt.X, pt.Y, 0, _host.Handle, IntPtr.Zero);
            NativeMethods.PostMessageW(_host.Handle, 0, IntPtr.Zero, IntPtr.Zero);

            switch ((uint)cmd)
            {
                case CmdOpen: OpenRequested?.Invoke(); break;
                case CmdSettings: SettingsRequested?.Invoke(); break;
                case CmdRebuild: RebuildRequested?.Invoke(); break;
                case CmdExit: ExitRequested?.Invoke(); break;
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        _host.RemoveHook(_hook);
        if (_added)
        {
            var data = BuildData();
            NativeMethods.Shell_NotifyIconW(NIM_DELETE, ref data);
            _added = false;
        }
        if (_icon != IntPtr.Zero) NativeMethods.DestroyIcon(_icon);
    }
}
