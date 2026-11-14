using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Media;
using RayLite.Models;
using RayLite.Native;

namespace RayLite.Services;

/// <summary>Static definition of one built-in command. Nothing here runs or allocates until a command is searched or executed.</summary>
internal sealed class SystemCommandDef
{
    public SystemCommandDef(
        string id, string name, string description, string[] keywords,
        int? stockIconId, string glyph, Color glyphColor, string? confirmText)
    {
        Id = id;
        Description = description;
        Keywords = keywords;
        StockIconId = stockIconId;
        Glyph = glyph;
        GlyphColor = glyphColor;
        ConfirmText = confirmText;
        Entry = new IndexEntry(name, id, EntryKind.SystemCommand);
    }

    public string Id { get; }
    public string Description { get; }
    public string[] Keywords { get; }

    /// <summary>SIID_* value for SHGetStockIconInfo, or null when Windows has no stock icon for the action.</summary>
    public int? StockIconId { get; }

    /// <summary>Segoe MDL2 Assets glyph used when there is no stock icon (or it cannot be loaded).</summary>
    public string Glyph { get; }

    public Color GlyphColor { get; }

    /// <summary>When set, the user must confirm before the command runs.</summary>
    public string? ConfirmText { get; }

    public IndexEntry Entry { get; }
}

internal static class SystemCommandService
{
    private const uint MB_YESNO = 0x00000004;
    private const uint MB_ICONWARNING = 0x00000030;
    private const uint MB_DEFBUTTON2 = 0x00000100;
    private const uint MB_SETFOREGROUND = 0x00010000;
    private const uint MB_TOPMOST = 0x00040000;
    private const int IDYES = 6;

    // SIID_* stock icon ids (shellapi.h)
    private const int SIID_RECYCLER = 31;
    private const int SIID_LOCK = 47;

    public static SystemCommandDef[] All { get; } =
    {
        new("lock", "Lock", "Lock this computer",
            new[] { "lock", "zamknut", "zamknúť" },
            SIID_LOCK, "\uE72E", Color.FromRgb(110, 200, 255), null),

        new("sleep", "Sleep", "Put computer to sleep",
            new[] { "sleep", "rezim spanku", "režim spánku", "uspat", "uspať" },
            null, "\uE708", Color.FromRgb(143, 168, 255), null),

        new("restart", "Restart", "Restart computer",
            new[] { "restart", "reboot", "restartovat", "reštartovať" },
            null, "\uE72C", Color.FromRgb(255, 214, 90),
            "Restart the computer now?\n\nUnsaved work in open applications may be lost."),

        new("shutdown", "Shutdown", "Shut down computer",
            new[] { "shutdown", "vypnut", "vypnúť", "power off" },
            null, "\uE7E8", Color.FromRgb(255, 107, 107),
            "Shut down the computer now?\n\nUnsaved work in open applications may be lost."),

        new("recyclebin", "Empty Recycle Bin", "Permanently delete everything in the Recycle Bin",
            new[] { "empty recycle bin", "trash", "bin", "kos", "kôš", "vysypat kos", "vysypať kôš" },
            SIID_RECYCLER, "\uE74D", Color.FromRgb(160, 220, 160),
            null), // set a confirmation text here if you want a prompt before emptying
    };

    public static SystemCommandDef? Find(string id)
    {
        foreach (SystemCommandDef command in All)
            if (command.Id == id) return command;
        return null;
    }

    public static string GetSubtitle(string id)
        => "System Command — " + (Find(id)?.Description ?? id);

    /// <summary>Runs the command. Returns false when it fails; true when it ran or the user declined the confirmation.</summary>
    public static bool Execute(string id)
    {
        SystemCommandDef? command = Find(id);
        if (command == null) return false;

        if (command.ConfirmText != null && !Confirm(command.ConfirmText))
            return true;

        try
        {
            switch (id)
            {
                case "lock":
                    return NativeMethods.LockWorkStation();

                case "sleep":
                    // bHibernate = false, bForce = false, bWakeupEventsDisabled = false
                    return NativeMethods.SetSuspendState(false, false, false);

                case "restart":
                    return RunShutdown("/r /t 0");

                case "shutdown":
                    return RunShutdown("/s /t 0");

                case "recyclebin":
                    return NativeMethods.SHEmptyRecycleBin(
                        IntPtr.Zero, null,
                        NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND) >= 0;

                default:
                    return false;
            }
        }
        catch (Exception ex) when (ex is Win32Exception or DllNotFoundException or EntryPointNotFoundException)
        {
            App.LogError(ex);
            return false;
        }
    }

    private static bool RunShutdown(string arguments)
    {
        var psi = new ProcessStartInfo("shutdown.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using Process? process = Process.Start(psi);
        return process != null;
    }

    /// <summary>Topmost, foreground Yes/No box defaulting to "No" (shown after the launcher window is hidden).</summary>
    private static bool Confirm(string text)
    {
        int result = NativeMethods.MessageBox(
            IntPtr.Zero, text, "RayLite",
            MB_YESNO | MB_ICONWARNING | MB_DEFBUTTON2 | MB_SETFOREGROUND | MB_TOPMOST);
        return result == IDYES;
    }
}
