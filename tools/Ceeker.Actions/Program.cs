using System.Diagnostics;
using System.Text.Json;
using Wormy.Infrastructure;
try
{
    if(args.Length==0||args[0] is "--help" or "help")
    {
        Console.WriteLine("Ceeker.Actions find <query> [--library <db>] [--limit <1..1000>]\nCeeker.Actions latest-download [--library <db>] [--limit <1..100>]\nCeeker.Actions search-in-app <query> [--app <Ceeker.App.exe>]\nCeeker.Actions quick-search [--app <Ceeker.App.exe>]\nRead-only local JSON actions for PowerShell, shortcuts and automation. Downloads are ordered by first indexed time.");return;
    }
    string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at>=0&&at+1<args.Length?args[at+1]:fallback;}
    var database=Option("--library",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Wormy","library.db"));
    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
    if(args[0] is "search-in-app" or "quick-search")
    {
        var app=Path.GetFullPath(Option("--app",Path.Combine(AppContext.BaseDirectory,"Ceeker.App.exe")));if(!File.Exists(app))throw new IOException("Ceeker.App.exe is missing. Use --app with its installed path.");
        var start=new ProcessStartInfo(app){UseShellExecute=false};start.ArgumentList.Add(args[0]=="quick-search"?"--quick-search":"--search");
        if(args[0]=="search-in-app"){if(args.Length<2||args[1].StartsWith("--"))throw new ArgumentException("Supply a search query.");start.ArgumentList.Add(args[1]);}
        Process.Start(start);Console.WriteLine("{\"launched\":true}");return;
    }
    if(!File.Exists(database))throw new IOException("No saved library. Connect a folder in Ceeker first or pass --library.");
    var limit=int.TryParse(Option("--limit","100"),out var parsed)?parsed:100;
    object result=args[0] switch
    {
        "find" when args.Length>1&&!args[1].StartsWith("--")=>await AutomationSearch.FindAsync(database,args[1],limit,timeout.Token),
        "latest-download"=>new { Files=await AutomationSearch.LatestDownloadsAsync(database,Math.Clamp(limit,1,100),timeout.Token),Ordering="First indexed by Ceeker" },
        _=>throw new ArgumentException("Unknown action. Run --help.")
    };
    Console.WriteLine(JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
}
catch(Exception ex){Console.Error.WriteLine(JsonSerializer.Serialize(new{error=ex.Message}));Environment.ExitCode=1;}
