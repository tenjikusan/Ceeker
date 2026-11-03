using Microsoft.UI.Xaml.Controls;
using Wormy.Core;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private readonly DuplicateReviewState duplicateReview = new();
    private void RetainDuplicateReviewAfterRecycle(IReadOnlySet<string> recycled)
    {
        if(recycled.Count==0 || !duplicateReview.Ready)return;
        var remaining=duplicateSelections.Select(selection=>new
        {
            Files=selection.Choices.Keys.Where(file=>!recycled.Contains(file.Path)).OrderBy(file=>file!=selection.Keep).ToArray(),
            Selected=selection.Choices.Where(pair=>pair.Value.IsChecked==true&&!recycled.Contains(pair.Key.Path)).Select(pair=>pair.Key.Path).ToHashSet(StringComparer.OrdinalIgnoreCase)
        }).Where(group=>group.Files.Length>1).ToArray();
        duplicateSelections.Clear();DuplicateGroupsPanel.Children.Clear();
        foreach(var group in remaining)
        {
            RenderDuplicateGroup(new DuplicateGroup(group.Files));
            var selection=duplicateSelections[^1];
            foreach(var pair in selection.Choices) pair.Value.IsChecked=pair.Key!=selection.Keep&&group.Selected.Contains(pair.Key.Path);
        }
        if(remaining.Length==0)DuplicateGroupsPanel.Children.Add(new TextBlock { Text="All displayed duplicate groups have been resolved. Retained originals remain in your folders.",TextWrapping=Microsoft.UI.Xaml.TextWrapping.Wrap });
        dashboardDuplicateGroups=remaining.Length;UpdateDashboard();UpdateCleanupButton();
    }
}
