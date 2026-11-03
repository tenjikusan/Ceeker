using Wormy.Core;
using Wormy.Infrastructure;

internal static class ScanSettingsChecks
{
    public static async Task CreateFixtureAsync(string home)
    {
        if (Directory.Exists(home)) throw new IOException("Use a new generated fixture directory.");
        var root = Path.Combine(home, "files"); Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "Visible.txt"), "Visible generated fixture.");
        await File.WriteAllTextAsync(Path.Combine(root, "Other.txt"), "Other generated fixture.");
        var hidden = Path.Combine(root, "Hidden.txt"); await File.WriteAllTextAsync(hidden, "Hidden generated fixture."); File.SetAttributes(hidden, FileAttributes.Hidden);
        var hiddenFolder = Path.Combine(root, "Hidden-folder"); Directory.CreateDirectory(hiddenFolder); File.SetAttributes(hiddenFolder, FileAttributes.Hidden);
        await File.WriteAllTextAsync(Path.Combine(hiddenFolder, "Nested.txt"), "Hidden folder generated fixture.");
        var cache = Path.Combine(root, "CaChE"); Directory.CreateDirectory(cache); await File.WriteAllTextAsync(Path.Combine(cache, "Cached.txt"), "Excluded folder generated fixture.");
        var index = new LibraryIndex(Path.Combine(home, "library.db"));
        await index.ReplaceRootAsync(root, (await FolderScanner.ScanAsync(root, CancellationToken.None)).Files, CancellationToken.None);
        Console.WriteLine($"Generated scanning fixture: {home}");
    }
    public static async Task RunAsync()
    {
        var home = Path.Combine(Path.GetTempPath(), "Wormy-scan-options-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(home, "files"); Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "visible.txt"), "Visible fixture");
            await File.WriteAllTextAsync(Path.Combine(root, "cache"), "Filename matching excluded folder remains visible");
            var hidden = Path.Combine(root, "hidden.txt"); await File.WriteAllTextAsync(hidden, "Hidden fixture"); File.SetAttributes(hidden, FileAttributes.Hidden);
            var hiddenFolder = Path.Combine(root, "hidden-folder"); Directory.CreateDirectory(hiddenFolder); File.SetAttributes(hiddenFolder, FileAttributes.Hidden);
            await File.WriteAllTextAsync(Path.Combine(hiddenFolder, "nested.txt"), "Hidden directory fixture");
            var cache = Path.Combine(root, "nested", "CaChE"); Directory.CreateDirectory(cache); await File.WriteAllTextAsync(Path.Combine(cache, "generated.txt"), "Excluded directory fixture");
            var original = await FolderScanner.ScanAsync(root, CancellationToken.None);
            if (original.Files.Count != 5 || original.Errors.Count != 0) throw new Exception("Legacy scan inclusion failed.");
            var options = new ScanOptions { IncludeHiddenFiles = false, ExcludedFolderNames = ["cache"] };
            var filtered = await FolderScanner.ScanAsync(root, CancellationToken.None, options);
            if (filtered.Files.Count != 2 || filtered.Errors.Count != 0 || filtered.Files.Any(f => f.Name == "hidden.txt" || f.Name == "nested.txt" || f.Name == "generated.txt")) throw new Exception("Hidden/excluded directory scope failed.");
            if ((await FolderScanner.ScanAsync(cache, CancellationToken.None, options)).Files.Count != 1) throw new Exception("Explicit excluded-name root failed.");
            if ((await FolderScanner.ScanAsync(hiddenFolder, CancellationToken.None, options)).Files.Count != 1) throw new Exception("Explicit hidden root failed.");
            var exact = await FolderScanner.ScanAsync(root, CancellationToken.None, new ScanOptions { IncludeHiddenFiles = false, ExcludedFolderNames = ["cache"], MaximumFiles = 2 });
            if (exact.Files.Count != 2 || exact.Errors.Count != 0) throw new Exception("Exact file-count boundary failed.");
            var limited = await FolderScanner.ScanAsync(root, CancellationToken.None, new ScanOptions { MaximumFiles = 2 });
            if (limited.Files.Count != 2 || limited.Errors.Count != 1 || !limited.Errors[0].Contains("limit")) throw new Exception("File cap did not report incomplete scan.");
            var index = new LibraryIndex(Path.Combine(home, "library.db")); await index.ReplaceRootAsync(root, original.Files, CancellationToken.None);
            try { await AutomaticRefresh.RefreshAsync(index, root, CancellationToken.None, new ScanOptions { MaximumFiles = 2 }); throw new Exception("Limited refresh accepted."); } catch (IOException) { }
            if ((await index.LoadAsync()).Files.Count != 5) throw new Exception("Limited refresh destroyed previous snapshot.");
            await AutomaticRefresh.RefreshAsync(index, root, CancellationToken.None, options);
            if ((await new LibraryIndex(Path.Combine(home, "library.db")).LoadAsync()).Files.Count != 2 || !File.Exists(hidden)) throw new Exception("Settings refresh persistence/original preservation failed.");
            var preferencePath = Path.Combine(home, "view.json");
            new Wormy.App.BrowsePreferences { IncludeHiddenFiles = false, MaximumScanFiles = 42, ExcludedFolderNames = [" cache ", "CACHE", "../bad", "*.tmp", "node_modules"] }.Save(preferencePath);
            var saved = Wormy.App.BrowsePreferences.Load(preferencePath);
            if (saved.IncludeHiddenFiles || saved.MaximumScanFiles != 42 || saved.ExcludedFolderNames.Count != 2) throw new Exception("Scan settings restart/normalization failed.");
            await File.WriteAllTextAsync(preferencePath, "{}"); saved = Wormy.App.BrowsePreferences.Load(preferencePath);
            if (!saved.IncludeHiddenFiles || saved.MaximumScanFiles != 100_000 || saved.ExcludedFolderNames.Count != 0) throw new Exception("Legacy scan defaults failed.");
            new Wormy.App.BrowsePreferences { MaximumScanFiles = int.MaxValue, ExcludedFolderNames = Enumerable.Range(0, 101).Select(i => $"folder-{i}").ToList() }.Save(preferencePath);
            saved = Wormy.App.BrowsePreferences.Load(preferencePath);
            if (saved.MaximumScanFiles != 1_000_000 || saved.ExcludedFolderNames.Count != 100) throw new Exception("Scanning preference upper bounds failed.");
            new Wormy.App.BrowsePreferences { MaximumScanFiles = -1 }.Save(preferencePath);
            if (Wormy.App.BrowsePreferences.Load(preferencePath).MaximumScanFiles != 1) throw new Exception("Scanning preference lower bound failed.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await FolderScanner.ScanAsync(root, cancelled.Token, options); throw new Exception("Scan settings cancellation failed."); } catch (OperationCanceledException) { }
            Console.WriteLine("PASS: hidden file/folder settings, exact case-insensitive exclusions, explicit roots, file-cap boundaries, incomplete-index preservation, restart/defaults/normalization and cancellation.");
        }
        finally { Directory.Delete(home, true); }
    }
}
