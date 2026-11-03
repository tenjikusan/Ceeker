using Wormy.Core;
internal sealed class DuplicateProgressCapture : IProgress<DuplicateProgress>
{
    public List<DuplicateGroup> Found { get; } = [];
    public int Completed { get; private set; }
    public void Report(DuplicateProgress value)
    {
        Completed = value.Completed;
        if(value.FoundGroup is { } group) Found.Add(group);
    }
}
