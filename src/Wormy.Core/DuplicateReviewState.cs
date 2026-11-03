namespace Wormy.Core;
public sealed class DuplicateReviewState
{
    public bool Ready { get; private set; }
    public bool Stale { get; private set; }
    public bool AllowsAutomaticScan => !Ready;
    public void Begin() { Ready=false; Stale=false; }
    public void Complete() { Ready=true; Stale=false; }
    public void LibraryChanged() { if(Ready) Stale=true; }
}
