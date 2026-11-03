namespace Wormy.Core;

public sealed record CleanupSuggestion(FileEntry File, IReadOnlyList<string> Reasons);
public static class CleanupSuggestions
{
    public static IReadOnlyList<CleanupSuggestion> Build(IEnumerable<FileEntry> files, long largeBytes, int downloadDays, int screenshotDays, DateTime nowUtc) => files
        .DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
        .Select(file => new CleanupSuggestion(file, Reasons(file, largeBytes, downloadDays, screenshotDays, nowUtc)))
        .Where(s => s.Reasons.Count > 0).OrderByDescending(s => s.File.Length).ThenBy(s => s.File.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    private static IReadOnlyList<string> Reasons(FileEntry file, long largeBytes, int downloadDays, int screenshotDays, DateTime nowUtc)
    {
        var reasons = new List<string>();
        if (file.Length >= largeBytes) reasons.Add("Large file");
        if (LibraryInsights.IsOldDownload(file, nowUtc, downloadDays)) reasons.Add("Old download");
        if (LibraryInsights.IsScreenshot(file) && LibraryInsights.IsOlder(file, nowUtc, screenshotDays)) reasons.Add("Old screenshot");
        if (IsOldInstaller(file, nowUtc)) reasons.Add("Old installer · review installation status");
        if (IsOldVideo(file, nowUtc, largeBytes)) reasons.Add("Large old video");
        return reasons;
    }
    public static bool IsOldInstaller(FileEntry file, DateTime nowUtc) => LibraryInsights.IsOlder(file, nowUtc, 30) &&
        (file.Extension is ".msi" or ".msix" or ".msixbundle" or ".appx" or ".appxbundle" || file.Extension == ".exe" && (file.Name.Contains("setup", StringComparison.OrdinalIgnoreCase) || file.Name.Contains("installer", StringComparison.OrdinalIgnoreCase)));
    public static bool IsOldVideo(FileEntry file, DateTime nowUtc, long largeBytes) => file.Category == "Videos" && file.Length >= largeBytes && LibraryInsights.IsOlder(file, nowUtc, 90);
    public static bool IsCandidate(FileEntry file,long largeBytes,int downloadDays,int screenshotDays,DateTime nowUtc) => file.Length>=largeBytes || LibraryInsights.IsOldDownload(file,nowUtc,downloadDays) || LibraryInsights.IsScreenshot(file)&&LibraryInsights.IsOlder(file,nowUtc,screenshotDays) || IsOldInstaller(file,nowUtc) || IsOldVideo(file,nowUtc,largeBytes);
}
