using Microsoft.Data.Sqlite;
using Wormy.Core;
namespace Wormy.Infrastructure;
public sealed record AutomationSearchResult(IReadOnlyList<FileEntry> Files,int TotalMatches,IReadOnlyList<string> Filters,IReadOnlyList<string> Warnings);
public static class AutomationSearch
{
    public static Task<AutomationSearchResult> FindAsync(string databasePath,string text,int limit=100,CancellationToken token=default)=>Task.Run(()=>
    {
        token.ThrowIfCancellationRequested();
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=Path.GetFullPath(databasePath),Mode=SqliteOpenMode.ReadOnly,Pooling=false }.ToString());db.Open();
        var query=SearchParser.Parse(text);
        using var command=db.CreateCommand();command.CommandText="SELECT DISTINCT Path,Name,Length,ModifiedTicks,DownloadSource,SourceHost,CreatedTicks,AccessedTicks,AddedTicks,CaptureMetadata FROM Files;";
        var files=new List<FileEntry>();
        using(var rows=command.ExecuteReader())while(rows.Read())
        {
            token.ThrowIfCancellationRequested();DateTime? Date(int at)=>rows.IsDBNull(at)?null:new DateTime(rows.GetInt64(at),DateTimeKind.Utc);
            files.Add(new(rows.GetString(0),rows.GetString(1),rows.GetInt64(2),new DateTime(rows.GetInt64(3),DateTimeKind.Utc)) { DownloadSource=rows.IsDBNull(4)?null:rows.GetString(4),SourceHost=rows.IsDBNull(5)?null:rows.GetString(5),CreatedUtc=Date(6),AccessedUtc=Date(7),AddedUtc=Date(8),CaptureMetadata=rows.GetInt64(9)!=0 });
        }
        var tags=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);command.CommandText="SELECT PathKey,Tag FROM Tags;";
        using(var rows=command.ExecuteReader())while(rows.Read()){if(!tags.TryGetValue(rows.GetString(0),out var list))tags[rows.GetString(0)]=list=[];list.Add(rows.GetString(1));}
        var content=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(query.Terms.Count>0)
        {
            command.CommandText="SELECT DISTINCT f.Path FROM ContentSearch s JOIN Content c ON c.PathKey=s.PathKey JOIN Files f ON f.PathKey=c.PathKey AND f.Length=c.Length AND f.ModifiedTicks=c.ModifiedTicks WHERE ContentSearch MATCH $terms;";
            command.Parameters.AddWithValue("$terms",string.Join(" AND ",query.Terms.Select(t=>"\""+t.Replace("\"","\"\"")+"\"")));
            using var rows=command.ExecuteReader();while(rows.Read()){token.ThrowIfCancellationRequested();content.Add(rows.GetString(0));}
        }
        var matches=files.DistinctBy(f=>f.Path,StringComparer.OrdinalIgnoreCase).Where(f=>FileSearch.Matches(query,f,tags.TryGetValue(f.Path,out var list)?list:[],content.Contains(f.Path))).OrderByDescending(f=>f.ModifiedUtc).ToArray();
        if(query.Devices.Count>0)query.Warnings.Add("This read-only action does not probe camera metadata. Use the desktop search for device-origin queries.");
        return new AutomationSearchResult(matches.Take(Math.Clamp(limit,1,1000)).ToArray(),matches.Length,query.Chips,query.Warnings);
    },token);
    public static async Task<IReadOnlyList<FileEntry>> LatestDownloadsAsync(string databasePath,int count=1,CancellationToken token=default)
    {
        // Keep all rows for ordering before applying the final recent-download bound.
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=Path.GetFullPath(databasePath),Mode=SqliteOpenMode.ReadOnly,Pooling=false }.ToString());await db.OpenAsync(token);
        using var command=db.CreateCommand();command.CommandText="SELECT Path,Name,Length,ModifiedTicks,AddedTicks FROM Files WHERE AddedTicks IS NOT NULL ORDER BY AddedTicks DESC;";
        var files=new List<FileEntry>();using var rows=await command.ExecuteReaderAsync(token);
        while(await rows.ReadAsync(token))files.Add(new(rows.GetString(0),rows.GetString(1),rows.GetInt64(2),new DateTime(rows.GetInt64(3),DateTimeKind.Utc)){AddedUtc=new DateTime(rows.GetInt64(4),DateTimeKind.Utc)});
        return DesktopFeatures.RecentDownloads(files,count);
    }
}
