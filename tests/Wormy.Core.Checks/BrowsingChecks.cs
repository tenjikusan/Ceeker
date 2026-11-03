using Wormy.Core;

internal static class BrowsingChecks
{
    public static void Run()
    {
        var now = DateTime.UtcNow;
        var recent = new FileEntry("C:/fixture/recent.txt", "recent.txt", 1, now.AddDays(-30));
        if (!FileBrowsing.InView(recent,"Recents",nowUtc:now) || FileBrowsing.InView(recent with { ModifiedUtc = now.AddDays(-30).AddTicks(-1) },"Recents",nowUtc:now) || FileBrowsing.InView(recent with { ModifiedUtc = now.AddTicks(1) },"Recents",nowUtc:now)) throw new Exception("Recents date boundaries failed.");
        FileEntry[] files = [new(@"C:\data\b.txt", "b.txt", 10, now.AddDays(-1)), new(@"C:\data\a.png", "a.png", 100_000_000, now), new(@"C:\database\Screenshot.png", "Screenshot.png", 5, now.AddDays(-2)), new(@"C:\data\Screen shot.txt", "Screen shot.txt", 99_999_999, now.AddDays(-3))];
        if (FileBrowsing.Sort(files, FileSort.Largest).First() != files[1] || FileBrowsing.Sort(files, FileSort.Smallest).First() != files[2]) throw new Exception("Size sorting failed.");
        if (FileBrowsing.Sort(files, FileSort.Newest).First() != files[1] || FileBrowsing.Sort(files, FileSort.Oldest).First() != files[3]) throw new Exception("Date sorting failed.");
        if (FileBrowsing.Sort(files, FileSort.NameAscending).First() != files[1] || FileBrowsing.Sort(files, FileSort.NameDescending).First() != files[2]) throw new Exception("Name sorting failed.");
        if (FileBrowsing.Sort(files, FileSort.KindAscending).First().Extension != ".png" || FileBrowsing.Sort(files, FileSort.KindDescending).First().Extension != ".txt") throw new Exception("Kind sorting failed.");
        if (!FileBrowsing.InView(files[1], "Large files") || FileBrowsing.InView(files[3], "Large files")) throw new Exception("Large-file threshold failed.");
        if (!FileBrowsing.InView(files[2], "Screenshots") || FileBrowsing.InView(files[3], "Screenshots")) throw new Exception("Screenshot view included a non-image.");
        if (!FileBrowsing.InFolder(files[0], @"c:\DATA\") || FileBrowsing.InFolder(files[2], @"C:\data")) throw new Exception("Pinned-folder boundary or casing failed.");
        if (!FileBrowsing.InView(files[1], "arbitrary folder name", @"C:\data") || FileBrowsing.InView(files[2], "arbitrary folder name", @"C:\data")) throw new Exception("Pinned-folder scope failed.");
        var ties = files.Select(f => f with { Name = "same.txt", Length = 1, ModifiedUtc = now }).ToArray();
        foreach (var sort in Enum.GetValues<FileSort>()) if (!FileBrowsing.Sort(ties, sort).Select(f => f.Path).SequenceEqual(FileBrowsing.Sort(ties.Reverse(), sort).Select(f => f.Path))) throw new Exception("Sort ties are unstable.");
        var folder = Path.Combine(Path.GetTempPath(), "Wormy-browse-" + Guid.NewGuid());
        var preferencesPath = Path.Combine(folder, "view.json");
        try
        {
            var preferences = new Wormy.App.BrowsePreferences { Sort = 4, TileWidth = 260, ListView = true, DarkTheme = true, TouchMode = true, AutomaticDuplicates = true, GettingStartedCompleted = true, PinnedFolders = [@"C:\data", @"c:\DATA", "relative"] };
            preferences.Save(preferencesPath);
            var saved = Wormy.App.BrowsePreferences.Load(preferencesPath);
            if (!saved.DarkTheme) throw new Exception("Dark theme preference was not saved.");
            if (!saved.TouchMode) throw new Exception("Touch mode preference was not saved.");
            if (!saved.AutomaticDuplicates) throw new Exception("Automatic duplicate preference was not saved.");
            if (!saved.GettingStartedCompleted) throw new Exception("Getting-started completion was not saved.");
            if (saved.Sort != 4 || saved.TileWidth != 260 || !saved.ListView || saved.PinnedFolders.Count != 1) throw new Exception("View preferences did not round-trip or normalize pins.");
            preferences.Sort = 99; preferences.TileWidth = 900; preferences.Save(preferencesPath);
            saved = Wormy.App.BrowsePreferences.Load(preferencesPath);
            if (saved.Sort != 7 || saved.TileWidth != 280 || File.Exists(preferencesPath + ".tmp")) throw new Exception("Preferences bounds or replacement failed.");
            File.WriteAllText(preferencesPath, "{}");
            if (Wormy.App.BrowsePreferences.Load(preferencesPath).DarkTheme) throw new Exception("Older preferences did not default to light theme.");
            if (Wormy.App.BrowsePreferences.Load(preferencesPath).TouchMode) throw new Exception("Older preferences did not default to mouse mode.");
            if (Wormy.App.BrowsePreferences.Load(preferencesPath).AutomaticDuplicates) throw new Exception("Automatic duplicate scanning should default off.");
            if (Wormy.App.BrowsePreferences.Load(preferencesPath).GettingStartedCompleted) throw new Exception("Older preferences incorrectly completed onboarding.");
            File.WriteAllText(preferencesPath, "malformed");
            try { Wormy.App.BrowsePreferences.Load(preferencesPath); throw new Exception("Corrupt preferences were silently accepted."); } catch (System.Text.Json.JsonException) { }
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        Console.WriteLine("Browsing checks passed: all sort directions, stable ties, smart-view boundaries and folder scope.");
    }
}
