using Wormy.Core;

internal static class ThumbnailChecks
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Wormy-thumbnails-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var files = new List<FileEntry>();
            foreach (var name in new[] { "a.txt", "b.txt", "c.txt" })
            { var path = Path.Combine(root, name); await File.WriteAllTextAsync(path, name); var info = new FileInfo(path); files.Add(new(path, name, info.Length, info.LastWriteTimeUtc)); }
            var calls = 0;
            var cache = new ThumbnailCache((_, _) => { calls++; return Task.FromResult<ThumbnailData?>(new([1,2,3])); }, 10, 6);
            await cache.GetAsync(files[0], CancellationToken.None); await cache.GetAsync(files[0] with { Path = files[0].Path.ToUpperInvariant() }, CancellationToken.None);
            if (calls != 1) throw new Exception("Thumbnail cache case-insensitive reuse failed.");
            await cache.GetAsync(files[1], CancellationToken.None); await cache.GetAsync(files[2], CancellationToken.None); await cache.GetAsync(files[0], CancellationToken.None);
            if (calls != 4) throw new Exception("Thumbnail memory-budget eviction failed.");
            await File.AppendAllTextAsync(files[0].Path, "changed");
            try { await cache.GetAsync(files[0], CancellationToken.None); throw new Exception("Stale thumbnail accepted."); } catch (IOException) { }
            File.Delete(files[1].Path);
            try { await cache.GetAsync(files[1], CancellationToken.None); throw new Exception("Missing thumbnail accepted."); } catch (IOException) { }
            var infoChanged = new FileInfo(files[0].Path); files[0] = files[0] with { Length = infoChanged.Length, ModifiedUtc = infoChanged.LastWriteTimeUtc };
            await cache.GetAsync(files[0], CancellationToken.None); if (calls != 5) throw new Exception("Changed metadata did not invalidate cache key.");
            var entryCalls = 0;
            var one = new ThumbnailCache((_, _) => { entryCalls++; return Task.FromResult<ThumbnailData?>(new([1])); }, 1, 100);
            await one.GetAsync(files[0], CancellationToken.None); await one.GetAsync(files[2], CancellationToken.None); await one.GetAsync(files[0], CancellationToken.None);
            if (entryCalls != 3) throw new Exception("Thumbnail entry-budget eviction failed.");
            var active = 0; var peak = 0;
            var slow = new ThumbnailCache(async (_, token) => { var count = Interlocked.Increment(ref active); peak = Math.Max(peak, count); try { await Task.Delay(100, token); return new ThumbnailData([1]); } finally { Interlocked.Decrement(ref active); } });
            await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => slow.GetAsync(files[2], CancellationToken.None)));
            if (peak > 2 || active != 0) throw new Exception("Thumbnail worker bound failed.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await slow.GetAsync(files[2], cancelled.Token); throw new Exception("Thumbnail cancellation ignored."); } catch (OperationCanceledException) { }
            var mutating = new ThumbnailCache(async (file, _) => { await File.AppendAllTextAsync(file.Path, "another edit"); return new ThumbnailData([1]); });
            try { await mutating.GetAsync(files[2], CancellationToken.None); throw new Exception("Changed-during-thumbnail accepted."); } catch (IOException) { }
            Console.WriteLine("PASS: thumbnail cache reuse/casing, byte/entry eviction, stale/missing/changed-during-read rejection, worker bounds and cancellation.");
        }
        finally { Directory.Delete(root, true); }
    }
}
