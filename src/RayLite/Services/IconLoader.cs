using System.Globalization;
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

/// <summary>Extracts icons natively (ExtractAssociatedIcon / SHGetStockIconInfo); small bounded cache, only visible results are loaded.</summary>
internal static class IconLoader
{
    private const int MaxCacheEntries = 96;
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ImageSource?> CommandIcons = new();

    private static readonly string PackagedAppFallback =
        Path.Combine(Environment.SystemDirectory, "ApplicationFrameHost.exe");

    public static ImageSource? Get(IndexEntry entry)
    {
        if (entry.Kind == EntryKind.SystemCommand) return GetCommandIcon(entry.Path);

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
        return FromIconHandle(handle);
    }

    /// <summary>Converts (and always destroys) an HICON.</summary>
    private static ImageSource? FromIconHandle(IntPtr handle)
    {
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

    // ---- System command icons: stock shell icon when Windows has one, otherwise a native-font glyph ----

    private static ImageSource? GetCommandIcon(string id)
    {
        if (CommandIcons.TryGetValue(id, out ImageSource? cached)) return cached;

        ImageSource? icon = null;
        SystemCommandDef? command = SystemCommandService.Find(id);
        if (command != null)
        {
            if (command.StockIconId is int siid) icon = LoadStockIcon(siid);
            icon ??= RenderGlyph(command.Glyph, command.GlyphColor);
        }

        CommandIcons[id] = icon; // at most five tiny entries
        return icon;
    }

    private static ImageSource? LoadStockIcon(int siid)
    {
        try
        {
            var info = new NativeMethods.SHSTOCKICONINFO
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.SHSTOCKICONINFO>(),
                szPath = string.Empty
            };

            return NativeMethods.SHGetStockIconInfo(siid, NativeMethods.SHGSI_ICON, ref info) == 0
                ? FromIconHandle(info.hIcon)
                : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Vector glyph from the Segoe MDL2 Assets system font (no bitmap assets, no System.Drawing).</summary>
    private static ImageSource? RenderGlyph(string glyph, Color color)
    {
        try
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            var text = new FormattedText(
                glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe MDL2 Assets"), 32, brush, 1.0);

            Geometry geometry = text.BuildGeometry(new Point(0, 0));
            if (geometry.IsEmpty()) return null;
            if (geometry.CanFreeze) geometry.Freeze();

            var drawing = new GeometryDrawing(brush, null, geometry);
            drawing.Freeze();

            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
