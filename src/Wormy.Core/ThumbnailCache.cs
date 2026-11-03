namespace Wormy.Core;

public sealed record ThumbnailData(byte[] Bytes, bool IsIcon = false);

public sealed class ThumbnailCache(Func<FileEntry, CancellationToken, Task<ThumbnailData?>> provider, int maximumEntries = 128, long maximumBytes = 32_000_000)
{
    private readonly SemaphoreSlim workers = new(2);
    private readonly object sync = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, ThumbnailData Data)>> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<(string Key, ThumbnailData Data)> recent = new();
    private long bytes;

    public async Task<ThumbnailData?> GetAsync(FileEntry file, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await workers.WaitAsync(token);
        try
        {
            await Task.Run(() => Validate(file), token);
            var key = $"{file.Path}\0{file.Length}\0{file.ModifiedUtc.Ticks}";
            lock (sync)
            {
                if (entries.TryGetValue(key, out var hit)) { recent.Remove(hit); recent.AddFirst(hit); return hit.Value.Data; }
            }
            var data = await provider(file, token);
            token.ThrowIfCancellationRequested();
            await Task.Run(() => Validate(file), token);
            if (data is null || data.Bytes.Length == 0 || data.Bytes.LongLength > maximumBytes) return data;
            lock (sync)
            {
                if (entries.Remove(key, out var old)) { recent.Remove(old); bytes -= old.Value.Data.Bytes.LongLength; }
                var item = recent.AddFirst((key, data)); entries[key] = item; bytes += data.Bytes.LongLength;
                while (entries.Count > Math.Max(1, maximumEntries) || bytes > maximumBytes)
                {
                    var last = recent.Last!; entries.Remove(last.Value.Key); bytes -= last.Value.Data.Bytes.LongLength; recent.RemoveLast();
                }
            }
            return data;
        }
        finally { workers.Release(); }
    }

    public static void Validate(FileEntry file)
    {
        var info = new FileInfo(file.Path);
        if (!info.Exists || info.Length != file.Length || info.LastWriteTimeUtc != file.ModifiedUtc) throw new IOException("File changed or is unavailable.");
        if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0) throw new IOException("Linked or offline files are unsupported.");
        for (var folder = info.Directory; folder is not null; folder = folder.Parent)
            if ((folder.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0) throw new IOException("Linked or offline folders are unsupported.");
    }
}
