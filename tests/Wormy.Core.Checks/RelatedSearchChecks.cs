using Wormy.Core;
using Wormy.Infrastructure;
internal static class RelatedSearchChecks
{
    public static async Task RunAsync()
    {
        var folder=Path.Combine(Path.GetTempPath(),"Wormy-related-"+Guid.NewGuid()); Directory.CreateDirectory(folder);
        try
        {
            var file=new FileEntry(Path.Combine(folder,"bill.txt"),"bill.txt",1,DateTime.UtcNow);
            var query=SearchParser.Parse("invoice");
            if(FileSearch.Matches(query,file,[],false) || !FileSearch.Matches(query,file,[],false,true)) throw new Exception("Related names or exact mode failed.");
            var index=new LibraryIndex(Path.Combine(folder,"library.db")); await index.ReplaceRootAsync(folder,[file],CancellationToken.None);
            await index.StoreContentAsync(file,"wireless passcode", "fixture",CancellationToken.None);
            if((await index.SearchContentAsync(["wifi","password"],CancellationToken.None,true)).Count!=1 || (await index.SearchContentAsync(["wifi","password"],CancellationToken.None)).Count!=0 || (await index.SearchContentAsync(["wifi","contract"],CancellationToken.None,true)).Count!=0) throw new Exception("Related FTS alternatives or AND grouping failed.");
            if(RelatedSearch.Alternatives("wireless passcode").Count!=1) throw new Exception("Phrase expanded.");
            Console.WriteLine("PASS: optional related-name matching, FTS OR alternatives/AND groups, exact mode and phrases.");
        }
        finally { Directory.Delete(folder,true); }
    }
}
