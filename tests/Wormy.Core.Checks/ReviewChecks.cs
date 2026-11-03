using Wormy.Core;

internal static class ReviewChecks
{
    public static async Task RunAsync()
    {
        var now = DateTime.UtcNow;
        var overlap = new FileEntry(Path.Combine("C:\\", "Downloads", "Screenshot-old.png"), "Screenshot-old.png", long.MaxValue, now.AddDays(-61));
        var recent = new FileEntry(Path.Combine("C:\\", "Downloads", "Recent.txt"), "Recent.txt", 1, now);
        var suggestions = CleanupSuggestions.Build([overlap, overlap with { Path = overlap.Path.ToUpperInvariant() }, recent], 100, 30, 30, now);
        if (suggestions.Count != 1 || suggestions[0].Reasons.Count != 3 || suggestions.Sum(s => (decimal)s.File.Length) != long.MaxValue) throw new Exception("Cleanup overlap total failed.");
        if (!FileBrowsing.InView(overlap, "Cleanup review", largeBytes:100, nowUtc:now) || FileBrowsing.InView(recent, "Cleanup review", largeBytes:100, nowUtc:now)) throw new Exception("Cleanup scope mismatch.");
        var root = Path.Combine(Path.GetTempPath(), "Wormy-review-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "photo.png"); await File.WriteAllTextAsync(path, "original");
            var info = new FileInfo(path); var file = new FileEntry(path, info.Name, info.Length, info.LastWriteTimeUtc);
            var request = await FileCleanup.PrepareAsync(file, CancellationToken.None); var calls = 0;
            await FileCleanup.RecycleAsync(request, _ => calls++, CancellationToken.None);
            if (calls != 1 || await File.ReadAllTextAsync(path) != "original") throw new Exception("General cleanup preparation changed original.");
            await File.WriteAllTextAsync(path, "modified"); File.SetLastWriteTimeUtc(path, file.ModifiedUtc);
            try { await FileCleanup.RecycleAsync(request, _ => calls++, CancellationToken.None); throw new Exception("Same-metadata edit accepted."); } catch(IOException) { }
            if (calls != 1) throw new Exception("Changed content reached recycle callback.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await FileCleanup.PrepareAsync(file, cancelled.Token); throw new Exception("Cleanup cancellation ignored."); } catch(OperationCanceledException) { }
            File.Delete(path);
            try { await FileCleanup.RecycleAsync(request, _ => calls++, CancellationToken.None); throw new Exception("Missing cleanup file accepted."); } catch(IOException) { }
            var samples = new List<FileEntry>();
            for (var i = 0; i < 4; i++) { var p = Path.Combine(root, $"{i}.png"); await File.WriteAllTextAsync(p, "sample"); var f = new FileInfo(p); samples.Add(new(p, f.Name, f.Length, f.LastWriteTimeUtc)); }
            var a = new PhotoFingerprint(0x123456789abcdef0, 1.5, 100, 120, 130); var b = a with { Hash = a.Hash ^ 1, Red = 105 };
            if (SimilarPhotos.Distance(a,b) != 1 || SimilarPhotos.Distance(a,a with { Hash = ~a.Hash }) is not null || SimilarPhotos.Distance(a,a with { Aspect = 2 }) is not null || SimilarPhotos.Distance(a,a with { Red = 200 }) is not null) throw new Exception("Similarity thresholds failed.");
            var result = await SimilarPhotos.FindAsync(samples.Concat([samples[0]]), (_,_) => Task.FromResult(a), CancellationToken.None, maximumImages:3, maximumPairs:2);
            if (result.Compared != 3 || result.Pairs.Count != 2 || !result.PairLimitReached || result.NotProcessed != 1) throw new Exception("Similarity caps/deduplication failed.");
            using var stop = new CancellationTokenSource(30);
            try { await SimilarPhotos.FindAsync(samples, async (_, token) => { await Task.Delay(1000,token); return a; }, stop.Token); throw new Exception("Active similarity cancellation ignored."); } catch(OperationCanceledException) { }
            result = await SimilarPhotos.FindAsync(samples, (_,_) => throw new IOException("Bad sample"), CancellationToken.None);
            if (result.Skipped != 4 || result.Pairs.Count != 0) throw new Exception("Skipped image accounting failed.");
            var rgba = new byte[288]; for(var i=0;i<72;i++) { rgba[i*4]=(byte)(i*3); rgba[i*4+1]=(byte)(i*3); rgba[i*4+2]=(byte)(i*3); rgba[i*4+3]=255; }
            var fingerprint = SimilarPhotos.Fingerprint(rgba, 1.5); if(fingerprint.Hash != 0) throw new Exception("Fingerprint gradient direction failed.");
            try { SimilarPhotos.Fingerprint(new byte[288],1); throw new Exception("Blank image sample accepted."); } catch(IOException) { }
            Console.WriteLine("PASS: general recycle review hash/cancellation/missing checks, deduplicated overlapping cleanup totals, similarity fingerprint/thresholds/caps/skip counts and active cancellation.");
        }
        finally { Directory.Delete(root,true); }
    }
}
