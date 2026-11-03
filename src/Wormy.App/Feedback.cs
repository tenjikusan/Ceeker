using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private void InitializeFeedback()
    {
        var toast = new InfoBar { IsOpen=false, IsClosable=true, HorizontalAlignment=HorizontalAlignment.Stretch };
        var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(5) };
        Shell.Loaded += (_,_) => { if (Status.Parent is Grid parent) { parent.Children.Add(toast); toast.Closed += (_,_)=>Status.Visibility=Visibility.Visible; } };
        timer.Tick += (_,_)=> { timer.Stop(); toast.IsOpen=false; Status.Visibility=Visibility.Visible; };
        Status.RegisterPropertyChangedCallback(TextBlock.TextProperty,(_,_)=>
        {
            var text=Status.Text;
            if (!new[] { "copied", "saved", "could not", "failed", "unable" }.Any(word=>text.Contains(word,StringComparison.OrdinalIgnoreCase))) return;
            toast.Message=text; toast.Severity=text.Contains("failed",StringComparison.OrdinalIgnoreCase)||text.Contains("unable",StringComparison.OrdinalIgnoreCase) ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
            toast.IsOpen=true; Status.Visibility=Visibility.Collapsed; timer.Stop(); timer.Start();
        });
        Closed += (_,_)=>timer.Stop();
    }
}
