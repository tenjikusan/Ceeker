using Wormy.Core;
internal static class DuplicateRetentionChecks
{
    public static void Run()
    {
        var review=new DuplicateReviewState();
        if(!review.AllowsAutomaticScan)throw new Exception("Initial automatic scan blocked.");
        review.Complete();review.LibraryChanged();review.LibraryChanged();
        if(!review.Ready||!review.Stale||review.AllowsAutomaticScan)throw new Exception("Refresh lost review or allowed replacement.");
        review.Begin();
        if(review.Ready||review.Stale||!review.AllowsAutomaticScan)throw new Exception("Explicit replacement did not reset review.");
        review.Complete();
        if(!review.Ready||review.Stale||review.AllowsAutomaticScan)throw new Exception("Completed review not pinned.");
        Console.WriteLine("PASS: completed duplicate review survives repeated library changes and blocks automatic replacement until explicit reset.");
    }
}
