namespace Wormy.Core;

public enum FileSort { Newest, Oldest, NameAscending, NameDescending, Largest, Smallest, KindAscending, KindDescending }

public static class FileBrowsing
{
    public static bool InFolder(FileEntry file, string folder) => file.Path.StartsWith(
        System.IO.Path.TrimEndingDirectorySeparator(folder) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public static bool InView(FileEntry file, string view, string? folder = null, long largeBytes = 100_000_000, int downloadDays = 30, int screenshotDays = 30, DateTime? nowUtc = null) => folder is not null ? InFolder(file, folder) : view switch
    {
        "Home" or "All files" => true,
        "Recents" => file.ModifiedUtc >= (nowUtc ?? DateTime.UtcNow).AddDays(-30) && file.ModifiedUtc <= (nowUtc ?? DateTime.UtcNow),
        "Downloads" => LibraryInsights.IsDownload(file),
        "Downloaded from" => file.DownloadSource is not null,
        "Large files" => file.Length >= largeBytes,
        "Screenshots" => LibraryInsights.IsScreenshot(file),
        "Old downloads" => LibraryInsights.IsOldDownload(file, nowUtc ?? DateTime.UtcNow, downloadDays),
        "Old screenshots" => LibraryInsights.IsScreenshot(file) && LibraryInsights.IsOlder(file, nowUtc ?? DateTime.UtcNow, screenshotDays),
        "Old installers" => CleanupSuggestions.IsOldInstaller(file, nowUtc ?? DateTime.UtcNow),
        "Old videos" => CleanupSuggestions.IsOldVideo(file, nowUtc ?? DateTime.UtcNow, largeBytes),
        "Cleanup review" => CleanupSuggestions.IsCandidate(file, largeBytes, downloadDays, screenshotDays, nowUtc ?? DateTime.UtcNow),
        _ => file.Category == view
    };

    public static IOrderedEnumerable<FileEntry> Sort(IEnumerable<FileEntry> files, FileSort sort) => (sort switch
    {
        FileSort.Oldest => files.OrderBy(f => f.ModifiedUtc),
        FileSort.NameAscending => files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
        FileSort.NameDescending => files.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase),
        FileSort.Largest => files.OrderByDescending(f => f.Length),
        FileSort.Smallest => files.OrderBy(f => f.Length),
        FileSort.KindAscending => files.OrderBy(f => f.Extension, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
        FileSort.KindDescending => files.OrderByDescending(f => f.Extension, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
        _ => files.OrderByDescending(f => f.ModifiedUtc)
    }).ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase);
}
