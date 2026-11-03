namespace Wormy.Core;

public sealed record ScanResult(IReadOnlyList<FileEntry> Files, IReadOnlyList<string> Errors);

public static class FolderScanner
{
    public static Task<ScanResult> ScanAsync(string root, CancellationToken cancellationToken, ScanOptions? options = null)
    {
        var includeHidden = options?.IncludeHiddenFiles ?? true;
        var maximum = Math.Clamp(options?.MaximumFiles ?? 100_000, 1, 1_000_000);
        var excluded = ScanOptions.NormalizeExclusions(options?.ExcludedFolderNames).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.Run(() =>
        {
            if(ProtectedFolders.IsVolumeMetadata(root)) return new ScanResult([], ["Protected Windows volume folders cannot be connected as library roots."]);
            var files = new List<FileEntry>();
            var errors = new List<string>();
            var pending = new Stack<string>();
            try { if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return new ScanResult([], ["Linked folder roots are unsupported."]); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new ScanResult([], [$"{root}: {ex.Message}"]); }
            pending.Push(root);
            while (pending.TryPop(out var directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if(ProtectedFolders.IsVolumeMetadata(path)) continue;
                        try
                        {
                            var attributes = File.GetAttributes(path);
                            if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                            if (!includeHidden && (attributes & FileAttributes.Hidden) != 0) continue;
                            if ((attributes & FileAttributes.Directory) != 0) { if (!excluded.Contains(Path.GetFileName(path))) pending.Push(path); continue; }
                            if (files.Count >= maximum) return new ScanResult(files, [$"File limit of {maximum:N0} reached; scan incomplete. Increase the limit or exclude folders."]);
                            var info = new FileInfo(path);
                            var origin = DownloadOrigins.Read(path);
                            files.Add(new FileEntry(path, info.Name, info.Length, info.LastWriteTimeUtc) { DownloadSource = origin?.Source, SourceHost = origin?.Host, CreatedUtc = info.CreationTimeUtc, AccessedUtc = info.LastAccessTimeUtc, CaptureMetadata = CaptureMetadata.IsCapture(path) });
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        { errors.Add($"{path}: {ex.Message}"); }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { errors.Add($"{directory}: {ex.Message}"); }
            }
            return new ScanResult(files, errors);
        }, cancellationToken);
    }
}
