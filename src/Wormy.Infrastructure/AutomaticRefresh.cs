using Wormy.Core;

namespace Wormy.Infrastructure;

public static class AutomaticRefresh
{
    public static async Task<bool> RefreshAsync(LibraryIndex index, string root, CancellationToken token, ScanOptions? options = null)
    {
        var scan = await FolderScanner.ScanAsync(root, token, options);
        token.ThrowIfCancellationRequested();
        if (scan.Errors.Count != 0) throw new IOException($"Folder unavailable or scan incomplete; previous library retained. {scan.Errors[0]}");
        var previous = await index.LoadRootFilesAsync(root);
        token.ThrowIfCancellationRequested();
        var byPath = previous.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);
        if (scan.Files.Count == previous.Count && scan.Files.All(f => byPath.TryGetValue(f.Path, out var old) && old.Length == f.Length && old.ModifiedUtc == f.ModifiedUtc && old.DownloadSource == f.DownloadSource && old.SourceHost == f.SourceHost)) return false;
        await index.ReplaceRootAsync(root, scan.Files, token);
        return true;
    }
}
