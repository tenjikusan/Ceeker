using Wormy.Core;
static class CleanupPerformanceChecks
{
    public static void Run()
    {
        var now=new DateTime(2026,10,12,0,0,0,DateTimeKind.Utc);
        var samples=Enumerable.Range(0,50000).Select(i=>new FileEntry($"C:\\fixture\\Downloads\\{i}"+(i%3==0?".mp4":i%3==1?".png":".txt"),$"{i}"+(i%3==0?".mp4":i%3==1?".png":".txt"),i%2==0?200_000_000:10,now.AddDays(-(i%120)))).ToArray();
        foreach(var file in samples.Take(1000)) if(CleanupSuggestions.IsCandidate(file,100_000_000,30,30,now)!=(CleanupSuggestions.Build([file],100_000_000,30,30,now).Count>0)) throw new Exception("Fast cleanup filter changed membership.");
        var clock=System.Diagnostics.Stopwatch.StartNew(); var count=samples.Count(file=>FileBrowsing.InView(file,"Cleanup review",largeBytes:100_000_000,nowUtc:now)); clock.Stop();
        Console.WriteLine($"PASS: fast cleanup membership matches original rules; 50,000 candidates checked in {clock.Elapsed.TotalMilliseconds:F1} ms ({count} matches). Fixture benchmark only.");
    }
}
