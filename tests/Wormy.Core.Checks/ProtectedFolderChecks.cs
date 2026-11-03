using Wormy.Core;
static class ProtectedFolderChecks
{
    public static async Task RunAsync()
    {
        var volume=Path.GetPathRoot(Path.GetFullPath("."))!;
        foreach(var name in new[]{"System Volume Information","$Recycle.Bin"}) {
            if(!ProtectedFolders.IsVolumeMetadata(Path.Combine(volume,name)) || !ProtectedFolders.IsVolumeMetadata(Path.Combine(volume,name.ToUpperInvariant()))) throw new Exception("Protected volume folder not identified.");
            if(ProtectedFolders.IsVolumeMetadata(Path.Combine(volume,"user-library",name))) throw new Exception("User folder falsely excluded.");
            var blocked=await FolderScanner.ScanAsync(Path.Combine(volume,name),CancellationToken.None);
            if(blocked.Errors.Count!=1 || blocked.Files.Count!=0) throw new Exception("Protected root was accepted.");
        }
        var fixture=Path.Combine(Path.GetTempPath(),"wormy-protected-folders-"+Guid.NewGuid());
        try {
            var nested=Path.Combine(fixture,"System Volume Information"); Directory.CreateDirectory(nested); await File.WriteAllTextAsync(Path.Combine(nested,"user.txt"),"generated user-folder fixture");
            var scan=await FolderScanner.ScanAsync(fixture,CancellationToken.None);
            if(scan.Errors.Count!=0 || scan.Files.Count!=1) throw new Exception("Nested user folder contents lost.");
            if(FolderInventory.Read(fixture,new(),CancellationToken.None).Folders.Count!=1) throw new Exception("Nested user folder hidden.");
        } finally { if(Directory.Exists(fixture)) Directory.Delete(fixture,true); }
        Console.WriteLine("PASS: protected volume metadata recognition, explicit-root rejection and nested same-name user-folder preservation.");
    }
}
