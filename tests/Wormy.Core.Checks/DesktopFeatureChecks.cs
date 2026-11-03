using System.Security.Cryptography;
using Wormy.Core;
using Wormy.Infrastructure;
internal static class DesktopFeatureChecks
{
    public static async Task RunAsync()
    {
        var root=Path.Combine(Path.GetTempPath(),"ceeker-desktop-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var modelRoot=Path.Combine(root,"Models");
            if(AiModelPack.IsAvailable(modelRoot))throw new Exception("Missing optional pack was accepted.");
            foreach(var relative in AiModelPack.RequiredFiles){var path=Path.Combine(modelRoot,relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllTextAsync(path,"Generated presence fixture, not a model.");}
            if(!AiModelPack.IsAvailable(modelRoot))throw new Exception("Complete optional pack presence failed.");
            File.WriteAllText(Path.Combine(modelRoot,AiModelPack.RequiredFiles[0]),"");
            if(AiModelPack.IsAvailable(modelRoot))throw new Exception("Empty optional model was accepted.");
            var downloads=Path.Combine(root,"Downloads");Directory.CreateDirectory(downloads);
            var now=DateTime.UtcNow;
            async Task<FileEntry> Make(string name,string contents,int days=0)
            {var path=Path.Combine(downloads,name);await File.WriteAllTextAsync(path,contents);File.SetLastWriteTimeUtc(path,now.AddDays(-days));var info=new FileInfo(path);return new(path,name,info.Length,info.LastWriteTimeUtc){AddedUtc=now.AddMinutes(-days)};}
            var first=await Make("Screenshot 2026.png","generated screenshot",100);var installer=await Make("setup.exe","inert generated installer",120);var document=await Make("meeting.txt","meeting document",1);
            var recent=DesktopFeatures.RecentDownloads([first,document,document,installer]);if(recent.Length!=3||recent[0].Name!="meeting.txt")throw new Exception("Arrival ordering or overlap deduplication failed.");
            var searches=DesktopFeatures.NormalizeSearches([new("a","Invoice","pdfs last week"),new("a","Duplicate ID","txt"),new("b"," ","txt")]);if(searches.Count!=1)throw new Exception("Saved search normalization failed.");
            var prefsPath=Path.Combine(root,"prefs.json");new Wormy.App.BrowsePreferences { SavedSearches=searches,MinimalStyle=true,TrayDownloads=false,RuleOrganizeScreenshots=false }.Save(prefsPath);
            var prefs=Wormy.App.BrowsePreferences.Load(prefsPath);if(prefs.SavedSearches.Count!=1||!prefs.MinimalStyle||prefs.TrayDownloads||prefs.RuleRecycleInstallers||prefs.RuleOrganizeScreenshots)throw new Exception("Desktop preference restart/off-by-default rules failed.");
            var disabled=CleanupRulePlanner.Plan([first,installer],new(downloads,false,false),now);if(disabled.Count!=0)throw new Exception("Disabled rules produced actions.");
            var actions=CleanupRulePlanner.Plan([first,installer,document],new(downloads,true,true,90),now);if(actions.Count!=2||actions.Count(a=>a.Kind=="File screenshot")!=1)throw new Exception("Rule age/type eligibility failed.");
            var sibling=first with { Path=Path.Combine(root,"DownloadsElsewhere",first.Name) };if(CleanupRulePlanner.Plan([sibling],new(downloads,true,true),now).Count!=0)throw new Exception("Rule escaped folder prefix.");
            var shot=actions.Single(a=>a.Kind=="File screenshot");await CleanupRulePlanner.MoveScreenshotAsync(shot,downloads,CancellationToken.None);
            if(File.Exists(first.Path)||!File.Exists(shot.Destination)||!File.Exists(installer.Path))throw new Exception("Screenshot filing or originals policy failed.");
            var moved=first with { Path=shot.Destination! };if(CleanupRulePlanner.Plan([moved],new(downloads,false,true),now).Count!=0)throw new Exception("Filed screenshot was processed twice.");
            var conflict=await Make(first.Name,"other screenshot");try{await CleanupRulePlanner.MoveScreenshotAsync(shot with { File=conflict },downloads,CancellationToken.None);throw new Exception("Destination overwrite allowed.");}catch(IOException){}
            if(!File.Exists(conflict.Path)||await File.ReadAllTextAsync(shot.Destination!)!="generated screenshot")throw new Exception("Conflict changed original files.");
            using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{await CleanupRulePlanner.MoveScreenshotAsync(shot with { File=conflict,Destination=Path.Combine(downloads,"Ceeker Screenshots","other",conflict.Name) },downloads,cancelled.Token);throw new Exception("Cancelled rule ran.");}catch(OperationCanceledException){}}
            var library=Path.Combine(root,"library.db");var index=new LibraryIndex(library);await index.ReplaceRootAsync(downloads,[document,installer],CancellationToken.None);await index.StoreContentAsync(document,"confidential invoice for project atlas","Text",CancellationToken.None);await index.SetTagsAsync(document.Path,["client-z"]);
            // The index assigns first-seen time itself. Use deterministic fixture times,
            // rather than depending on two inserts receiving distinct clock ticks.
            using(var db=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={library};Pooling=False"))
            {
                await db.OpenAsync();using var cmd=db.CreateCommand();
                cmd.CommandText="UPDATE Files SET AddedTicks=$ticks WHERE Name=$name";
                cmd.Parameters.AddWithValue("$ticks",now.AddDays(-1).Ticks);cmd.Parameters.AddWithValue("$name",document.Name);await cmd.ExecuteNonQueryAsync();
                cmd.Parameters["$ticks"].Value=now.Ticks;cmd.Parameters["$name"].Value=installer.Name;await cmd.ExecuteNonQueryAsync();
            }
            var before=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(library)));
            var content=await AutomationSearch.FindAsync(library,"atlas");var tagged=await AutomationSearch.FindAsync(library,"client-z");var latest=await AutomationSearch.LatestDownloadsAsync(library);
            if(content.Files.Count!=1||tagged.Files.Count!=1||latest.Count!=1||latest[0].Path!=installer.Path)throw new Exception("Read-only automation content/tag/latest retrieval failed.");
            if(before!=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(library))))throw new Exception("Read-only action changed the saved index.");
            var absent=Path.Combine(root,"missing.db");
            try{await AutomationSearch.FindAsync(absent,"txt");throw new Exception("Missing database accepted.");}catch(Microsoft.Data.Sqlite.SqliteException){}
            if(File.Exists(absent))throw new Exception("Read-only action created a database.");
            Console.WriteLine("PASS: saved-search/preferences restart, first-seen downloads, scoped/off-by-default rules, screenshot filing/conflicts/cancellation, and read-only automation content/tag/latest retrieval.");
        }
        finally{Directory.Delete(root,true);}
    }
}


