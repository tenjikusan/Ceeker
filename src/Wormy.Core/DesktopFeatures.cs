namespace Wormy.Core;

public sealed record SavedSearchDefinition(string Id, string Name, string Query, string Category = "All files", string? Folder = null, string Source = "All sources", bool Contents = true, bool CurrentView = false);

public static class DesktopFeatures
{
    public static List<SavedSearchDefinition> NormalizeSearches(IEnumerable<SavedSearchDefinition>? searches) =>
        (searches ?? []).Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Name) && !string.IsNullOrWhiteSpace(s.Query))
        .Select(s => s with { Id = string.IsNullOrWhiteSpace(s.Id) ? Guid.NewGuid().ToString("N") : s.Id, Name = s.Name.Trim()[..Math.Min(64,s.Name.Trim().Length)], Query = s.Query[..Math.Min(1000,s.Query.Length)] })
        .DistinctBy(s => s.Id).Take(50).ToList();

    // AddedUtc is first observed by Ceeker, not an assertion of browser download time.
    public static FileEntry[] RecentDownloads(IEnumerable<FileEntry> files, int count = 10) => files
        .Where(LibraryInsights.IsDownload).Where(f => f.AddedUtc is not null)
        .DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase).OrderByDescending(f => f.AddedUtc)
        .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Take(Math.Clamp(count,1,100)).ToArray();
}

public sealed record CleanupRuleOptions(string Folder, bool RecycleInstallers, bool OrganizeScreenshots, int InstallerAgeDays = 90);
public sealed record CleanupRuleAction(FileEntry File, string Kind, string? Destination);

public static class CleanupRulePlanner
{
    public static IReadOnlyList<CleanupRuleAction> Plan(IEnumerable<FileEntry> files, CleanupRuleOptions rule, DateTime now)
    {
        if(string.IsNullOrWhiteSpace(rule.Folder)) return [];
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rule.Folder));
        if(string.Equals(folder,Path.TrimEndingDirectorySeparator(Path.GetPathRoot(folder)!),StringComparison.OrdinalIgnoreCase)) return [];
        var targetRoot = Path.Combine(folder,"Ceeker Screenshots");
        var result = new List<CleanupRuleAction>();
        foreach(var file in files.DistinctBy(f=>f.Path,StringComparer.OrdinalIgnoreCase))
        {
            if(!FileBrowsing.InFolder(file,folder) || FileBrowsing.InFolder(file,targetRoot) || ProtectedFolders.IsVolumeMetadata(Path.GetDirectoryName(file.Path)!)) continue;
            if(rule.RecycleInstallers && CleanupSuggestions.IsOldInstaller(file,now) && file.ModifiedUtc < now.AddDays(-Math.Clamp(rule.InstallerAgeDays,30,3650)))
                result.Add(new(file,"Recycle installer",null));
            else if(rule.OrganizeScreenshots && LibraryInsights.IsScreenshot(file))
                result.Add(new(file,"File screenshot",Path.Combine(targetRoot,file.ModifiedUtc.ToLocalTime().ToString("yyyy-MM"),file.Name)));
        }
        return result.OrderBy(a=>a.File.Path,StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static async Task MoveScreenshotAsync(CleanupRuleAction action, string allowedFolder, CancellationToken token)
    {
        if(action.Kind!="File screenshot" || action.Destination is null)throw new IOException("Not a screenshot filing action.");
        var folder=Path.GetFullPath(allowedFolder); var destination=Path.GetFullPath(action.Destination);
        if(!FileBrowsing.InFolder(action.File,folder) || !FileBrowsing.InFolder(new FileEntry(destination,"",0,DateTime.MinValue),Path.Combine(folder,"Ceeker Screenshots")))throw new IOException("Action escaped the selected folder.");
        var request=await FileCleanup.PrepareAsync(action.File,token);
        var parent=Path.GetDirectoryName(destination)!;
        for(var check=new DirectoryInfo(parent);check is not null;check=check.Parent)
            if(check.Exists && (check.Attributes & (FileAttributes.ReparsePoint|FileAttributes.Offline))!=0)throw new IOException("Linked/offline destination rejected.");
        Directory.CreateDirectory(parent);
        // Rehash immediately before moving, using the same safety path as reviewed cleanup.
        await FileCleanup.RecycleAsync(request,path=>File.Move(path,destination,false),token);
    }
}
