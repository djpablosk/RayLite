using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using RayLite.Models;
using RayLite.Native;

namespace RayLite.Services;

/// <summary>Extracts icons natively via ExtractAssociatedIcon; small bounded cache (only visible results are ever loaded).</summary>
internal static class IconLoader
{
    private const int MaxCacheEntries = 96;
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string PackagedAppFallback =
        Path.Combine(Environment.SystemDirectory, "ApplicationFrameHost.exe");

    public static ImageSource? Get(IndexEntry entry)
    {
        string path;
        string key;

        switch (entry.Kind)
        {
            case EntryKind.UwpApp:
                path = PackagedAppFallback;
                key = "<uwp>";
                break;
            case EntryKind.Folder:
                path = entry.Path;
                key = "<folder>";
                break;
            case EntryKind.Application:
                path = entry.Path;
                key = entry.Path;
                break;
            default:
                path = entry.Path;
                string ext = Path.GetExtension(entry.Path);
                key = ext.Length == 0 ? "<noext>"
                    : ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                      ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                      ext.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
                      ext.Equals(".url", StringComparison.OrdinalIgnoreCase)
                        ? entry.Path
                        : ext;
                break;
        }

        if (Cache.TryGetValue(key, out ImageSource? cached)) return cached;

        ImageSource? icon = Extract(path);
        if (Cache.Count >= MaxCacheEntries) Cache.Clear();
        Cache[key] = icon;
        return icon;
    }

    private static ImageSource? Extract(string path)
    {
        var buffer = new StringBuilder(path, Math.Max(1024, path.Length + 1));
        ushort index = 0;
        IntPtr handle = NativeMethods.ExtractAssociatedIconW(IntPtr.Zero, buffer, ref index);
        if (handle == IntPtr.Zero) return null;

        try
        {
            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
            source.Freeze();
            return source;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or COMException)
        {
            return null;
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}
