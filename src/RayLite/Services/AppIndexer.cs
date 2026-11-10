using System.IO;
using System.Diagnostics;
using System.Text;
using RayLite.Models;

namespace RayLite.Services;

/// <summary>Indexes installed applications: Start Menu shortcuts, packaged (UWP) apps, common .exe folders, core Windows tools.</summary>
public static class AppIndexer
{
    private static readonly string[] NoiseTokens =
    {
        "unins", "uninstall", "crash", "updat", "helper", "setup", "install",
        "repair", "report", "elevat", "broker", "migrat", "cleanup"
    };

    private static readonly string[] SkippedDirectories = { "Common Files", "WindowsApps", "dotnet", "Reference Assemblies", "Windows NT" };

    private static readonly (string RelativePath, string Name)[] SystemTools =
    {
        ("notepad.exe", "Notepad"),
        ("calc.exe", "Calculator"),
        ("cmd.exe", "Command Prompt"),
        ("mspaint.exe", "Paint"),
        ("taskmgr.exe", "Task Manager"),
        ("control.exe", "Control Panel"),
        ("msinfo32.exe", "System Information"),
        ("resmon.exe", "Resource Monitor"),
        ("charmap.exe", "Character Map"),
        ("mmc.exe", "Microsoft Management Console"),
        (@"WindowsPowerShell\v1.0\powershell.exe", "Windows PowerShell"),
    };

    public static List<IndexEntry> Index()
    {
        var results = new List<IndexEntry>(512);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string name, string path, EntryKind kind)
        {
            name = name.Trim();
            if (name.Length > 0 && seen.Add(name)) results.Add(new IndexEntry(name, path, kind));
        }

        TryRun(() => AddSystemTools(Add));
        TryRun(() => AddStartMenuShortcuts(Add));
        TryRun(() => AddPackagedApps(Add));
        TryRun(() => AddExecutables(Add));

        return results;
    }

    private static void TryRun(Action action)
    {
        try { action(); }
        catch (Exception ex) { App.LogError(ex); }
    }

    private static void AddSystemTools(Action<string, string, EntryKind> add)
    {
        string system32 = Environment.SystemDirectory;
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        foreach (var (relative, name) in SystemTools)
        {
            string path = Path.Combine(system32, relative);
            if (File.Exists(path)) add(name, path, EntryKind.Application);
        }

        string regedit = Path.Combine(windows, "regedit.exe");
        if (File.Exists(regedit)) add("Registry Editor", regedit, EntryKind.Application);
    }

    private static void AddStartMenuShortcuts(Action<string, string, EntryKind> add)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        string[] roots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
        };

        foreach (string root in roots)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;

            foreach (string file in Directory.EnumerateFiles(root, "*.lnk", options))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) continue;
                add(name, file, EntryKind.Application);
            }
        }
    }

    private static void AddPackagedApps(Action<string, string, EntryKind> add)
    {
        var psi = new ProcessStartInfo("powershell.exe")
        {
            Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " +
                        "\"[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " +
                        "Get-StartApps | ForEach-Object { $_.Name + '|' + $_.AppID }\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi);
        if (process == null) return;

        var read = process.StandardOutput.ReadToEndAsync();
        if (!read.Wait(TimeSpan.FromSeconds(25)))
        {
            try { process.Kill(true); } catch (InvalidOperationException) { }
            return;
        }

        foreach (string line in read.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int sep = line.LastIndexOf('|');
            if (sep <= 0) continue;

            string name = line[..sep].Trim();
            string appId = line[(sep + 1)..].Trim();

            // Packaged apps always have the form "<PackageFamilyName>!<AppId>"; desktop apps are covered by Start Menu shortcuts.
            if (appId.Contains('!')) add(name, appId, EntryKind.UwpApp);
        }
    }

    private static void AddExecutables(Action<string, string, EntryKind> add)
    {
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false };
        string[] roots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
        };

        foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;

            foreach (string level1 in Directory.EnumerateDirectories(root, "*", options))
            {
                if (IsSkippedDirectory(level1)) continue;
                AddExecutablesIn(level1, options, add);

                foreach (string level2 in Directory.EnumerateDirectories(level1, "*", options))
                    AddExecutablesIn(level2, options, add);
            }
        }
    }

    private static void AddExecutablesIn(string directory, EnumerationOptions options, Action<string, string, EntryKind> add)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.exe", options))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (!IsNoise(name)) add(name, file, EntryKind.Application);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static bool IsSkippedDirectory(string path)
    {
        string name = Path.GetFileName(path);
        foreach (string skipped in SkippedDirectories)
            if (name.Equals(skipped, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsNoise(string name)
    {
        foreach (string token in NoiseTokens)
            if (name.Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
