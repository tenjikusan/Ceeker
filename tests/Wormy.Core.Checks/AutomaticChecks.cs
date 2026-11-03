using Wormy.Core;
using Wormy.Infrastructure;

internal static class AutomaticChecks
{
    private static async Task WaitForChange(FolderChanges changes, string root)
    {
        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (DateTime.UtcNow < deadline)
        {
            if (changes.TakeReady().Any(p => p.Equals(root, StringComparison.OrdinalIgnoreCase))) return;
            await Task.Delay(100);
        }
        throw new Exception("Folder watcher notification did not arrive.");
    }
    public static async Task RunAsync()
    {
        var home = Path.Combine(Path.GetTempPath(), "Wormy-auto-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(home, "files"); Directory.CreateDirectory(root);
        try
        {
            var note = Path.Combine(root, "note.txt"); await File.WriteAllTextAsync(note, "previous indexed phrase");
            var index = new LibraryIndex(Path.Combine(home, "library.db"));
            var scan = await FolderScanner.ScanAsync(root, CancellationToken.None);
            await index.ReplaceRootAsync(root, scan.Files, CancellationToken.None);
            await index.StoreContentAsync(scan.Files.Single(), "previous indexed phrase", "Test", CancellationToken.None);
            using var changes = new FolderChanges(); changes.SetRoots([root]);
            await WaitForChange(changes, root);
            if (await AutomaticRefresh.RefreshAsync(index, root, CancellationToken.None)) throw new Exception("Unchanged automatic refresh republished snapshot.");
            var nested = Path.Combine(root, "nested"); Directory.CreateDirectory(nested);
            var added = Path.Combine(nested, "added.txt"); await File.WriteAllTextAsync(added, "new file");
            await WaitForChange(changes, root);
            if (!await AutomaticRefresh.RefreshAsync(index, root, CancellationToken.None) || (await index.LoadAsync()).Files.Count != 2) throw new Exception("Automatic create failed.");
            await File.WriteAllTextAsync(note, "changed contents with a longer length");
            await WaitForChange(changes, root);
            await AutomaticRefresh.RefreshAsync(index, root, CancellationToken.None);
            if ((await index.SearchContentAsync(["previous indexed phrase"], CancellationToken.None)).Count != 0) throw new Exception("Automatic edit retained stale contents.");
            var renamed = Path.Combine(nested, "renamed.txt"); File.Move(added, renamed);
            await WaitForChange(changes, root); await AutomaticRefresh.RefreshAsync(index, root, CancellationToken.None);
            if (!(await index.LoadAsync()).Files.Any(f => f.Path == renamed)) throw new Exception("Automatic rename failed.");
            File.Delete(renamed); await WaitForChange(changes, root); await AutomaticRefresh.RefreshAsync(index, root, CancellationToken.None);
            if ((await new LibraryIndex(Path.Combine(home, "library.db")).LoadAsync()).Files.Count != 1) throw new Exception("Automatic deletion/restart failed.");
            Directory.Move(root, root + "-offline");
            try { await AutomaticRefresh.RefreshAsync(index, root, CancellationToken.None); throw new Exception("Unavailable root accepted."); } catch (IOException) { }
            if ((await index.LoadAsync()).Files.Count != 1) throw new Exception("Unavailable root destroyed saved metadata.");
            Directory.Move(root + "-offline", root);
            changes.SetRoots([]); await File.WriteAllTextAsync(note, "paused changes");
            await Task.Delay(1800); if (changes.TakeReady().Count != 0) throw new Exception("Disconnected watcher remained active.");
            changes.SetRoots([root]); await WaitForChange(changes, root); await AutomaticRefresh.RefreshAsync(index, root, CancellationToken.None);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            try { await AutomaticRefresh.RefreshAsync(index, root, cancel.Token); throw new Exception("Automatic cancellation ignored."); } catch (OperationCanceledException) { }
            Console.WriteLine("PASS: real folder watcher create/edit/rename/delete, unchanged snapshot suppression, stale-content invalidation, restart, unavailable-root preservation, pause/reconnect and cancellation.");
        }
        finally { Directory.Delete(home, true); }
    }
}
