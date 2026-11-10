using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using RayLite.Native;

namespace RayLite.Services;

/// <summary>
/// Owns a message-only window used for the global hotkey (WM_HOTKEY) and the tray icon callbacks.
/// </summary>
public sealed class HotKeyManager : IDisposable
{
    private const int HotKeyId = 0x4C4C;

    private readonly HwndSource _source;
    private bool _registered;

    public event Action? HotKeyPressed;

    public IntPtr Handle => _source.Handle;

    public HotKeyManager()
    {
        _source = new HwndSource(0, 0, 0, 0, 0, "RayLite.MessageWindow", NativeMethods.HWND_MESSAGE);
        _source.AddHook(WndProc);
    }

    internal void AddHook(HwndSourceHook hook) => _source.AddHook(hook);

    internal void RemoveHook(HwndSourceHook hook) => _source.RemoveHook(hook);

    public bool TryRegister(string hotkey, out string error)
    {
        error = string.Empty;

        if (!HotKeyParser.TryParse(hotkey, out uint modifiers, out uint vk))
        {
            error = $"\"{hotkey}\" is not a valid hotkey. Use a combination such as Ctrl+Space or Ctrl+Shift+K.";
            return false;
        }

        Unregister();

        if (!NativeMethods.RegisterHotKey(Handle, HotKeyId, modifiers | NativeMethods.MOD_NOREPEAT, vk))
        {
            string reason = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            error = $"Could not register {hotkey}: {reason} The combination may already be used by another application.";
            return false;
        }

        _registered = true;
        return true;
    }

    public void Unregister()
    {
        if (!_registered) return;
        NativeMethods.UnregisterHotKey(Handle, HotKeyId);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotKeyId)
        {
            handled = true;
            HotKeyPressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}

public static class HotKeyParser
{
    public static bool TryParse(string? text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        Key key = Key.None;
        foreach (string raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "alt": modifiers |= NativeMethods.MOD_ALT; break;
                case "ctrl":
                case "control": modifiers |= NativeMethods.MOD_CONTROL; break;
                case "shift": modifiers |= NativeMethods.MOD_SHIFT; break;
                case "win":
                case "windows": modifiers |= NativeMethods.MOD_WIN; break;
                default:
                    if (key != Key.None) return false;
                    if (raw.Length > 1 && char.IsDigit(raw[0])) return false;
                    string name = raw.Length == 1 && char.IsDigit(raw[0]) ? "D" + raw : raw;
                    if (!Enum.TryParse(name, true, out key) || key == Key.None || !Enum.IsDefined(key)) return false;
                    break;
            }
        }

        if (modifiers == 0 || key == Key.None) return false;

        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0) return false;

        vk = (uint)virtualKey;
        return true;
    }

    public static string Format(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>(5);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
