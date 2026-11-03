namespace Wormy.Core;
public sealed record FolderItem(string Path,string Name,bool Empty);
public sealed record FolderInventoryResult(IReadOnlyList<FolderItem> Folders,int Unavailable,bool Limited);
public static class FolderInventory
{
    public static FolderInventoryResult Read(string root,ScanOptions options,CancellationToken token)
    {
        if(ProtectedFolders.IsVolumeMetadata(root)) throw new IOException("Protected Windows volume folders cannot be browsed.");
        var folders=new List<FolderItem>(); var unavailable=0;
        if((File.GetAttributes(root)&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0) throw new IOException("Linked or offline folders cannot be browsed.");
        var excluded=ScanOptions.NormalizeExclusions(options.ExcludedFolderNames).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var path in Directory.EnumerateDirectories(root))
        {
            token.ThrowIfCancellationRequested();
            if(ProtectedFolders.IsVolumeMetadata(path)) continue;
            if(folders.Count==1000) return new(folders,unavailable,true);
            try
            {
                var attributes=File.GetAttributes(path);
                if((attributes&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0 || !options.IncludeHiddenFiles&&(attributes&FileAttributes.Hidden)!=0 || excluded.Contains(System.IO.Path.GetFileName(path))) continue;
                var empty=!Directory.EnumerateFileSystemEntries(path).Any();
                folders.Add(new(path,System.IO.Path.GetFileName(path),empty));
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { unavailable++; }
        }
        return new(folders.OrderBy(f=>f.Name,StringComparer.OrdinalIgnoreCase).ToArray(),unavailable,false);
    }
}
public sealed record AllocationSummary(int Measured,int Unavailable,int NotProcessed,decimal LogicalBytes,decimal AllocatedBytes);
public static class FolderAllocation
{
    public static AllocationSummary Measure(IEnumerable<FileEntry> files,CancellationToken token)
    {
        var selected=files.DistinctBy(f=>f.Path,StringComparer.OrdinalIgnoreCase).ToArray();
        var measured=0; var unavailable=0; decimal logical=0,allocated=0;
        var identities=new HashSet<string>(StringComparer.Ordinal);
        foreach(var file in selected.Take(10000))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                ThumbnailCache.Validate(file);
                var details=FileInspection.Read(file.Path);
                if(details.AllocatedBytes is not { } bytes) { unavailable++; continue; }
                ThumbnailCache.Validate(file); measured++; logical+=details.Length;
                if(details.FileIdentity is null || identities.Add(details.FileIdentity)) allocated+=bytes;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { unavailable++; }
        }
        return new(measured,unavailable,Math.Max(0,selected.Length-10000),logical,allocated);
    }
    public static AllocationSummary MeasureDirectory(string root,CancellationToken token)
    {
        var pending=new Stack<string>(); pending.Push(root); var files=new List<FileEntry>(); var errors=0; var directories=0; var limited=0;
        while(pending.TryPop(out var folder))
        {
            token.ThrowIfCancellationRequested(); if(++directories>1000) { limited++; break; }
            try
            {
                if((File.GetAttributes(folder)&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0) { errors++; continue; }
                foreach(var path in Directory.EnumerateFileSystemEntries(folder))
                {
                    token.ThrowIfCancellationRequested(); if(files.Count==10000) { limited++; break; }
                    try
                    {
                        var attributes=File.GetAttributes(path); if((attributes&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0) { errors++; continue; }
                        if((attributes&FileAttributes.Directory)!=0) pending.Push(path);
                        else { var info=new FileInfo(path); files.Add(new(path,info.Name,info.Length,info.LastWriteTimeUtc)); }
                    }
                    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { errors++; }
                }
                if(limited>0) break;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { errors++; }
        }
        var result=Measure(files,token); return result with { Unavailable=result.Unavailable+errors,NotProcessed=result.NotProcessed+limited };
    }
}
