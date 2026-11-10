using System.IO;
using RayLite.Models;

namespace RayLite.Services;

/// <summary>Rebuilds the search index on a low-priority background thread; coalesces overlapping requests.</summary>
public sealed class IndexManager
{
    private readonly SearchEngine _engine;
    private readonly object _gate = new();
    private bool _running;
    private AppSettings? _queued;

    public event Action? IndexUpdated;

    public IndexManager(SearchEngine engine) => _engine = engine;

    public void RebuildAsync(AppSettings settings)
    {
        lock (_gate)
        {
            if (_running)
            {
                _queued = settings;
                return;
            }
            _running = true;
        }

        new Thread(() => Worker(settings))
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "RayLite.Indexer"
        }.Start();
    }

    private void Worker(AppSettings first)
    {
        AppSettings? current = first;
        while (current != null)
        {
            try
            {
                List<IndexEntry> apps = AppIndexer.Index();
                List<IndexEntry> files = FileIndexer.Index(current);

                var all = new IndexEntry[apps.Count + files.Count];
                apps.CopyTo(all, 0);
                files.CopyTo(all, apps.Count);
                _engine.SetIndex(all);

                IndexUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                App.LogError(ex);
            }

            MemoryTrimmer.Trim();

            lock (_gate)
            {
                current = _queued;
                _queued = null;
                if (current == null) _running = false;
            }
        }
    }
}
