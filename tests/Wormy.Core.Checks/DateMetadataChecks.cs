using Wormy.Core;
using Wormy.Infrastructure;

internal static class DateMetadataChecks
{
    public static async Task RunAsync()
    {
        var fixture=Path.Combine(Path.GetTempPath(),"Wormy-dates-"+Guid.NewGuid());
        Directory.CreateDirectory(fixture);
        try
        {
            var root=Path.Combine(fixture,"data"); var child=Path.Combine(root,"nested"); Directory.CreateDirectory(child);
            var path=Path.Combine(child,"note.txt"); await File.WriteAllTextAsync(path,"metadata fixture");
            var created=new DateTime(2025,1,2,0,0,0,DateTimeKind.Utc); var accessed=created.AddDays(2);
            var file=new FileEntry(path,"note.txt",16,DateTime.UtcNow) { CreatedUtc=created,AccessedUtc=accessed };
            var database=Path.Combine(fixture,"library.db"); var index=new LibraryIndex(database);
            await index.ReplaceRootAsync(root,[file],CancellationToken.None);
            var first=(await index.LoadAsync()).Files.Single();
            if(first.CreatedUtc!=created || first.AccessedUtc!=accessed || first.AddedUtc is null) throw new Exception("Stored date metadata missing.");
            await index.ReplaceRootAsync(child,[file],CancellationToken.None);
            await index.ReplaceRootAsync(root,[file with { ModifiedUtc=file.ModifiedUtc.AddHours(1) }],CancellationToken.None);
            var restored=(await new LibraryIndex(database).LoadAsync()).Files.Single();
            if(restored.AddedUtc!=first.AddedUtc) throw new Exception("Refresh or overlap changed first-added date.");
            await index.RemoveRootAsync(root);
            if((await index.LoadAsync()).Files.Single().AddedUtc!=first.AddedUtc) throw new Exception("Overlap disconnect lost first-added date.");
            Console.WriteLine("PASS: date metadata restart, first-added preservation across refresh/overlap/disconnect.");
        }
        finally { Directory.Delete(fixture,true); }
    }
}
