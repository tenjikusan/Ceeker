namespace Wormy.Core;

/// Coalesces watcher notifications; callers periodically reconcile to recover missed events.
public sealed class FolderChanges(string? excludedFile = null) : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, FileSystemWatcher> watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (DateTime First, DateTime Last)> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> failed = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    public void SetRoots(IEnumerable<string> paths)
    {
        lock (gate)
        {
            if (disposed) return;
            var roots = paths.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var path in watchers.Keys.Where(p => !roots.Contains(p) || failed.Contains(p)).ToArray())
            { watchers[path].Dispose(); watchers.Remove(path); }
            foreach (var path in pending.Keys.Where(p => !roots.Contains(p)).ToArray()) pending.Remove(path);
            failed.Clear();
            foreach (var path in roots.Where(p => !watchers.ContainsKey(p)))
            {
                try
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked roots unsupported.");
                    var watcher = new FileSystemWatcher(path) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size };
                    watcher.Created += (_, e) => Changed(path, e.FullPath);
                    watcher.Changed += (_, e) => Changed(path, e.FullPath);
                    watcher.Deleted += (_, e) => Changed(path, e.FullPath);
                    watcher.Renamed += (_, e) => { Changed(path, e.OldFullPath); Changed(path, e.FullPath); };
                    watcher.Error += (_, _) => { lock (gate) { if (!disposed && watchers.ContainsKey(path)) { failed.Add(path); Queue(path); } } };
                    watchers.Add(path, watcher); watcher.EnableRaisingEvents = true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                { failed.Add(path); }
                Queue(path); // Reconcile startup and newly connected roots, including offline ones.
            }
        }
    }

    private void Changed(string root, string path)
    {
        if (excludedFile is not null && (path.Equals(excludedFile, StringComparison.OrdinalIgnoreCase) || path.Equals(excludedFile + "-wal", StringComparison.OrdinalIgnoreCase) || path.Equals(excludedFile + "-shm", StringComparison.OrdinalIgnoreCase))) return;
        lock (gate) { if (!disposed && watchers.ContainsKey(root)) Queue(root); }
    }
    private void Queue(string path)
    {
        var now = DateTime.UtcNow;
        pending[path] = (pending.TryGetValue(path, out var previous) ? previous.First : now, now);
    }
    public void Reconcile(IEnumerable<string> paths)
    { lock (gate) { if (!disposed) foreach (var path in paths) Queue(Path.GetFullPath(path)); } }
    public IReadOnlyList<string> TakeReady()
    {
        lock (gate)
        {
            var now = DateTime.UtcNow;
            var ready = pending.Where(p => now - p.Value.Last >= TimeSpan.FromSeconds(1.5) || now - p.Value.First >= TimeSpan.FromSeconds(10)).Select(p => p.Key).ToArray();
            foreach (var path in ready) pending.Remove(path);
            return ready;
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return; disposed = true;
            foreach (var watcher in watchers.Values) watcher.Dispose();
            watchers.Clear(); pending.Clear(); failed.Clear();
        }
    }
}
