using Wormy.Core;
internal static class FeatureBatchChecks
{
    public static void Run()
    {
        var now = DateTime.UtcNow;
        var installer = new FileEntry("C:/fixture/setup.exe","setup.exe",100,now.AddDays(-31));
        var video = new FileEntry("C:/fixture/old.mp4","old.mp4",200_000_000,now.AddDays(-91));
        if (!CleanupSuggestions.IsOldInstaller(installer,now) || CleanupSuggestions.IsOldInstaller(installer with { Name="application.exe" },now) || CleanupSuggestions.IsOldInstaller(installer with { ModifiedUtc=now },now)) throw new Exception("Installer review rules failed.");
        if (!CleanupSuggestions.IsOldVideo(video,now,100_000_000) || CleanupSuggestions.IsOldVideo(video with { Length=100 },now,100_000_000) || CleanupSuggestions.IsOldVideo(video with { ModifiedUtc=now },now,100_000_000)) throw new Exception("Old video rules failed.");
        var suggestions=CleanupSuggestions.Build([installer,video,video],100_000_000,30,30,now);
        if (suggestions.Count!=2 || suggestions.Single(s=>s.File==video).Reasons.Count!=2 || !FileBrowsing.InView(installer,"Cleanup review",nowUtc:now)) throw new Exception("Overlapping cleanup cards not deduplicated.");
        var sample=new byte[32*32*4]; var brighter=new byte[sample.Length]; var unrelated=new byte[sample.Length];
        for(var y=0;y<32;y++) for(var x=0;x<32;x++)
        {
            var i=(y*32+x)*4; var value=(byte)(50+(x*7+y*11+x*y)%130);
            for(var c=0;c<3;c++) { sample[i+c]=value; brighter[i+c]=(byte)(value+20); unrelated[i+c]=(byte)(255-value); }
            sample[i+3]=brighter[i+3]=unrelated[i+3]=255;
        }
        var original=new PhotoFingerprint(123,1,100,100,100,SimilarPhotos.PerceptualHash(sample),SimilarPhotos.PerceptualHash(sample,true));
        if(SimilarPhotos.Distance(original,original with { PerceptualHash=SimilarPhotos.PerceptualHash(brighter), CenterHash=SimilarPhotos.PerceptualHash(brighter,true) }) is null || SimilarPhotos.Distance(original,original with { PerceptualHash=SimilarPhotos.PerceptualHash(unrelated), CenterHash=SimilarPhotos.PerceptualHash(unrelated,true) }) is not null) throw new Exception("Brightness resilience/unrelated contrast matching failed.");
        Console.WriteLine("PASS: installer/video age and size boundaries, overlapping cleanup totals, perceptual brightness resilience and unrelated-image rejection.");
    }
}
