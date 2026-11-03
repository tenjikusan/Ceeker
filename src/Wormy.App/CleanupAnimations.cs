using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private async Task AnimateCleanupAsync()
    {
        if(closed||DuplicateGroupsPanel.Children.Count==0) return;
        var storyboard=new Storyboard();
        var animation=new DoubleAnimation { From=1,To=0,Duration=new Duration(TimeSpan.FromMilliseconds(180)),EnableDependentAnimation=true };
        Storyboard.SetTarget(animation,DuplicateGroupsPanel); Storyboard.SetTargetProperty(animation,"Opacity"); storyboard.Children.Add(animation);
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); storyboard.Completed+=(_,_)=>completion.TrySetResult();
        try { storyboard.Begin(); await completion.Task.WaitAsync(TimeSpan.FromSeconds(1)); }
        catch(TimeoutException) { }
        finally { storyboard.Stop(); DuplicateGroupsPanel.Opacity=1; }
    }
}
