namespace Wormy.Core;

public sealed record StorageCategory(string Name, int Count, decimal Bytes);
public sealed record SourceSummary(string Name, int Count, decimal Bytes);

public static class LibraryInsights
{
    public static IReadOnlyList<SourceSummary> Sources(IEnumerable<FileEntry> files) => files
        .DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
        .GroupBy(f => f.DownloadSource ?? "Unknown source", StringComparer.OrdinalIgnoreCase)
        .Select(g => new SourceSummary(g.Key, g.Count(), g.Sum(f => (decimal)Math.Max(0, f.Length))))
        .OrderByDescending(g => g.Count).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    public static bool IsDownload(FileEntry file) => file.Path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
        .SkipLast(1).Any(part => part.Equals("Downloads", StringComparison.OrdinalIgnoreCase));
    public static IReadOnlyList<StorageCategory> Storage(IEnumerable<FileEntry> files) => files
        .DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
        .GroupBy(f => f.Category).Select(g => new StorageCategory(g.Key, g.Count(), g.Sum(f => (decimal)Math.Max(0, f.Length))))
        .OrderByDescending(g => g.Bytes).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public static bool IsScreenshot(FileEntry file) => file.Category == "Images" &&
        (file.CaptureMetadata || new[] { "Screenshot", "Screen shot", "Screen capture", "ScreenCapture", "Snip", "Screen recording" }.Any(prefix => file.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

    public static bool IsOldDownload(FileEntry file, DateTime nowUtc, int days) =>
        IsDownload(file) && IsOlder(file, nowUtc, days);

    public static bool IsOlder(FileEntry file, DateTime nowUtc, int days) => file.ModifiedUtc < nowUtc.AddDays(-Math.Clamp(days, 1, 3650));
    public static string FormatBytes(decimal bytes) => bytes >= 1_000_000_000_000m ? $"{bytes / 1_000_000_000_000m:F1} TB"
        : bytes >= 1_000_000_000m ? $"{bytes / 1_000_000_000m:F1} GB" : bytes >= 1_000_000m ? $"{bytes / 1_000_000m:F1} MB"
        : bytes >= 1000 ? $"{bytes / 1000:F1} KB" : $"{bytes:0} B";
}
