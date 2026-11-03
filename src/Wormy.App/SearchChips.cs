using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private IReadOnlyList<string> displayedQueryChips=[];
    private void SetFilterChips(IEnumerable<string> values)
    {
        displayedQueryChips=values.ToArray(); RenderFilterChips();
    }
    private void RenderFilterChips()
    {
        if(FilterChips is null) return;
        FilterChips.Children.Clear();
        var available=Math.Max(240,BrowseScroll.ActualWidth-40); var used=0d;
        var row=new StackPanel { Orientation=Orientation.Horizontal,Spacing=6 }; FilterChips.Children.Add(row);
        foreach(var original in displayedQueryChips)
        {
            var label=original.Split('·')[0].Trim();
            if(label.StartsWith("Type: ")) label=label[6..];
            if(label.StartsWith("Modified ")) label=label[9..];
            var quote=label.IndexOf('“'); if(quote>=0) label=label[quote..];
            var text=new TextBlock { Text=label,FontSize=12,FontFamily=new FontFamily("Segoe UI"),Foreground=AppTheme.Brush("PurpleTextBrush"),TextTrimming=TextTrimming.CharacterEllipsis };
            text.Measure(new Windows.Foundation.Size(double.PositiveInfinity,40));
            var width=Math.Min(Math.Min(available,320),text.DesiredSize.Width+24);
            if(used>0 && used+width+6>available) { row=new StackPanel { Orientation=Orientation.Horizontal,Spacing=6 };FilterChips.Children.Add(row);used=0; }
            var chip=new Border { Child=text,Width=width,CornerRadius=new CornerRadius(14),Padding=new Thickness(12,6,12,6),Background=AppTheme.Brush("TintBrush") };
            ToolTipService.SetToolTip(chip,original);row.Children.Add(chip);used+=width+6;
        }
    }
}
