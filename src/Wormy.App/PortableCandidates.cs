using System.Diagnostics;
using Wormy.Core;
namespace Wormy.App;
public static class PortableCandidates
{
    public static IReadOnlyList<InstalledApp> Read(IEnumerable<FileEntry> files,IReadOnlyList<InstalledApp> registered,CancellationToken token)
    {
        var result=new List<InstalledApp>();
        foreach(var file in files.Where(f=>f.Extension==".exe").Take(1000))
        {
            token.ThrowIfCancellationRequested();
            if(registered.Any(app=>app.InstallLocation is { Length:>0 } location&&Path.IsPathFullyQualified(location)&&FileBrowsing.InFolder(file,location))) continue;
            try
            {
                ThumbnailCache.Validate(file);
                var metadata=FileVersionInfo.GetVersionInfo(file.Path);
                var name=string.IsNullOrWhiteSpace(metadata.ProductName) ? file.Name : metadata.ProductName;
                result.Add(new("file:"+file.Path,name,metadata.FileVersion??"",(metadata.CompanyName??"Unknown publisher")+" · file metadata, unverified","Portable/executable candidate · not in a known install folder",file.Length,ExecutablePath:file.Path));
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
        }
        return result;
    }
}
