using Wormy.Core;
using Wormy.Infrastructure;

internal static class InsightChecks
{
    public static void Run()
    {
        var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
        var old = new FileEntry(@"C:\data\Downloads\nested\note.txt", "note.txt", 1_000_000, now.AddDays(-31));
        var edge = old with { Path = @"C:\data\Downloads\edge.txt", ModifiedUtc = now.AddDays(-30) };
        if (!FileBrowsing.InView(edge, "Downloads") || FileBrowsing.InView(old with { Path = @"C:\data\MyDownloads\note.txt" }, "Downloads") || FileBrowsing.InView(old with { Path = @"C:\data\Downloads", Name = "Downloads" }, "Downloads")) throw new Exception("Downloads folder scope failed.");
        var sources = LibraryInsights.Sources([old with { DownloadSource = "GitHub" }, old with { Path = old.Path.ToUpperInvariant(), DownloadSource = "GitHub" }, edge, edge with { Path = @"C:\data\other.txt", DownloadSource = "github", Length = long.MaxValue }]);
        if (sources.Sum(s => s.Count) != 3 || sources.Single(s => s.Name == "GitHub").Count != 2 || sources.Sum(s => s.Bytes) != (decimal)long.MaxValue + 2_000_000m || sources.Single(s => s.Name == "Unknown source").Count != 1 || LibraryInsights.Sources([]).Count != 0) throw new Exception("Source aggregation deduplication/case/overflow/unknown failed.");
        if (!FileBrowsing.InView(old, "Old downloads", nowUtc: now) || FileBrowsing.InView(edge, "Old downloads", nowUtc: now)) throw new Exception("Download age boundary failed.");
        if (FileBrowsing.InView(old with { Path = @"C:\data\MyDownloads\note.txt" }, "Old downloads", nowUtc: now) || FileBrowsing.InView(old with { Path = @"C:\data\Downloads", Name = "Downloads" }, "Old downloads", nowUtc: now)) throw new Exception("Download folder boundary failed.");
        if (!FileBrowsing.InView(old with { Path = @"C:\data\downloads\note.txt" }, "Old downloads", nowUtc: now)) throw new Exception("Download folder casing failed.");
        var shot = old with { Name = "Screenshot-old.png", Path = @"C:\data\Screenshot-old.png" };
        if (!FileBrowsing.InView(shot, "Old screenshots", nowUtc: now) || FileBrowsing.InView(shot with { ModifiedUtc = now }, "Old screenshots", nowUtc: now) || FileBrowsing.InView(old with { Name = "Screenshot.txt" }, "Old screenshots", nowUtc: now)) throw new Exception("Old screenshot scope failed.");
        if (!FileBrowsing.InView(old, "Large files", largeBytes: 1_000_000) || FileBrowsing.InView(old, "Large files", largeBytes: 1_000_001)) throw new Exception("Configured size boundary failed.");
        var summary = LibraryInsights.Storage([old, old with { Path = old.Path.ToUpperInvariant() }, shot, edge with { Length = long.MaxValue }, edge with { Path = @"C:\data\extra.txt", Length = long.MaxValue }]);
        if (summary.Sum(c => c.Count) != 4 || summary.Sum(c => c.Bytes) != 2m * long.MaxValue + 2_000_000m) throw new Exception("Storage deduplication/overflow failed.");
        if (LibraryInsights.Storage([]).Count != 0 || LibraryInsights.FormatBytes(0) != "0 B") throw new Exception("Empty storage failed.");
        var path = Path.Combine(Path.GetTempPath(), "Wormy-insights-" + Guid.NewGuid(), "view.json");
        try
        {
            var preferences = new Wormy.App.BrowsePreferences { LargeFileMB = 500, DownloadAgeDays = 60, ScreenshotAgeDays = 90 }; preferences.Save(path);
            var saved = Wormy.App.BrowsePreferences.Load(path);
            if (saved.LargeFileMB != 500 || saved.DownloadAgeDays != 60 || saved.ScreenshotAgeDays != 90) throw new Exception("Review settings persistence failed.");
            File.WriteAllText(path, "{}"); saved = Wormy.App.BrowsePreferences.Load(path);
            if (saved.LargeFileMB != 100 || saved.DownloadAgeDays != 30 || saved.ScreenshotAgeDays != 30) throw new Exception("Legacy preferences defaults failed.");
            preferences.LargeFileMB = int.MaxValue; preferences.DownloadAgeDays = 0; preferences.ScreenshotAgeDays = -1; preferences.Save(path); saved = Wormy.App.BrowsePreferences.Load(path);
            if (saved.LargeFileMB != 1_000_000 || saved.DownloadAgeDays != 1 || saved.ScreenshotAgeDays != 1) throw new Exception("Review settings bounds failed.");
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
        Console.WriteLine("Insight checks passed: age/size boundaries, folder scope, screenshot types, deduplicated storage, overflow, settings restart/defaults/bounds.");
    }

    public static async Task CreateFixtureAsync(string root)
    {
        if (Directory.Exists(root)) throw new IOException("Use a new fixture directory.");
        var downloads = Path.Combine(root, "Downloads"); Directory.CreateDirectory(downloads);
        var old = Path.Combine(downloads, "Old-note.txt"); await File.WriteAllTextAsync(old, "Generated old download."); File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-61));
        var recent = Path.Combine(downloads, "Recent.txt"); await File.WriteAllTextAsync(recent, "Generated recent note.");
        var shot = Path.Combine(downloads, "Screenshot-old.png"); await File.WriteAllBytesAsync(shot, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=")); File.SetLastWriteTimeUtc(shot, DateTime.UtcNow.AddDays(-91));
        using (var large = File.Create(Path.Combine(downloads, "Large.bin"))) large.SetLength(2_000_000);
        var index = new LibraryIndex(Path.Combine(root, "library.db"));
        await index.ReplaceRootAsync(downloads, (await FolderScanner.ScanAsync(downloads, CancellationToken.None)).Files, CancellationToken.None);
        new Wormy.App.BrowsePreferences { LargeFileMB = 1 }.Save(Path.Combine(root, "library.db.view.json"));
        Console.WriteLine($"Insight fixture: {Path.Combine(root, "library.db")}");
    }
}
