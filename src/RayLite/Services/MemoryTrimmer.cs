using System.IO;
using System.Runtime;
using System.Windows;
using System.Windows.Threading;
using RayLite.Native;

namespace RayLite.Services;

/// <summary>Forces a compacting GC and trims the working set; called whenever the launcher goes idle.</summary>
internal static class MemoryTrimmer
{
    public static void Trim()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        NativeMethods.SetProcessWorkingSetSize(NativeMethods.GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
    }

    public static void ScheduleTrim()
    {
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(Trim));
    }
}
