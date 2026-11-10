using System.IO;
using System.IO.Enumeration;
using System.Runtime.InteropServices;
using RayLite.Models;
using RayLite.Native;

namespace RayLite.Services;

/// <summary>Breadth-first, capped indexer for user files and folders.</summary>
public static class FileIndexer
{
    private static readonly HashSet<string> SkippedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp", ".ini", ".dll", ".sys", ".db", ".log", ".dat", ".cache", ".bak", ".pyc", ".class", ".obj"
    };

    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    public static List<IndexEntry> Index(AppSettings settings)
    {
        var result = new List<IndexEntry>(Math.Min(settings.MaxIndexedFiles, 8192));

        var excludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var excludedPaths = new List<string>();
        foreach (string raw in settings.ExcludedFolders)
        {
            string item = raw.Trim();
            if (item.Length == 0) continue;

            if (item.IndexOfAny(new[] { '\\', '/', ':' }) >= 0)
            {
                try { excludedPaths.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(item))); }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
            }
            else
            {
                excludedNames.Add(item);
            }
        }

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
        };

        var queue = new Queue<(string Directory, int Depth)>();
        foreach (string root in ResolveRoots(settings))
            queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            var (directory, depth) = queue.Dequeue();

            try
            {
                var entries = new FileSystemEnumerable<(string Full, string Name, bool IsDirectory)>(directory, Transform, options);
                foreach (var (full, name, isDirectory) in entries)
                {
                    if (IsExcluded(full, name, excludedNames, excludedPaths)) continue;

                    if (isDirectory)
                    {
                        result.Add(new IndexEntry(name, full, EntryKind.Folder));
                        if (depth + 1 < settings.MaxDepth) queue.Enqueue((full, depth + 1));
                    }
                    else
                    {
                        if (ShouldSkipFile(name)) continue;
                        result.Add(new IndexEntry(name, full, EntryKind.File));
                    }

                    if (result.Count >= settings.MaxIndexedFiles) return result;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        return result;
    }

    private static (string Full, string Name, bool IsDirectory) Transform(ref FileSystemEntry entry)
        => (entry.ToFullPath(), entry.FileName.ToString(), entry.IsDirectory);

    private static bool ShouldSkipFile(string name)
    {
        if (name.StartsWith("~$", StringComparison.Ordinal)) return true;
        return SkippedExtensions.Contains(Path.GetExtension(name));
    }

    private static bool IsExcluded(string full, string name, HashSet<string> names, List<string> paths)
    {
        if (names.Contains(name)) return true;
        foreach (string p in paths)
        {
            if (full.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(p + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static List<string> ResolveRoots(AppSettings settings)
    {
        var candidates = new List<string>();

        if (settings.IndexDefaultFolders)
        {
            candidates.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            candidates.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            string? downloads = GetDownloadsFolder();
            if (downloads != null) candidates.Add(downloads);
        }

        candidates.AddRange(settings.IncludedFolders);

        var normalized = new List<string>();
        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
                if (Directory.Exists(full)) normalized.Add(full);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
        }

        // Drop duplicates and roots nested inside another root.
        var ordered = normalized.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p.Length).ToList();
        var kept = new List<string>();
        foreach (string path in ordered)
        {
            bool nested = kept.Any(k => path.StartsWith(k + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            if (!nested) kept.Add(path);
        }
        return kept;
    }

    private static string? GetDownloadsFolder()
    {
        try
        {
            if (NativeMethods.SHGetKnownFolderPath(DownloadsFolderId, 0, IntPtr.Zero, out IntPtr ptr) == 0 && ptr != IntPtr.Zero)
            {
                try { return Marshal.PtrToStringUni(ptr); }
                finally { Marshal.FreeCoTaskMem(ptr); }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }

        string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return Directory.Exists(fallback) ? fallback : null;
    }
}
