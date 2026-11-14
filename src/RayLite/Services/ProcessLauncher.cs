using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using RayLite.Models;

namespace RayLite.Services;

internal static class ProcessLauncher
{
    public static bool Open(IndexEntry entry)
    {
        if (entry.Kind == EntryKind.SystemCommand)
            return SystemCommandService.Execute(entry.Path);

        try
        {
            ProcessStartInfo psi = entry.Kind == EntryKind.UwpApp
                ? new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\" + entry.Path) { UseShellExecute = true }
                : new ProcessStartInfo(entry.Path) { UseShellExecute = true };

            if (entry.Kind == EntryKind.Application && entry.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                psi.WorkingDirectory = Path.GetDirectoryName(entry.Path) ?? string.Empty;

            using Process? process = Process.Start(psi);
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return false;
        }
    }
}
