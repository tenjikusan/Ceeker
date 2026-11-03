using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Wormy.App;

public static class AppTheme
{
    public static bool Minimal { get; set; }
    public static SolidColorBrush Brush(string name) => (SolidColorBrush)Application.Current.Resources[name];
    public static void Apply(bool dark)
    {
        var colors = new Dictionary<string, string>
        {
            ["PageBrush"] = dark ? "191C1F" : "EEF0F2",
            ["SidebarBrush"] = dark ? "202428" : "FAFBFC",
            ["CardBrush"] = dark ? "272C30" : "FFFFFF",
            ["LineBrush"] = dark ? "474E54" : "D8DCE0",
            ["TextBrush"] = dark ? "F4F5F6" : "22272B",
            ["MutedBrush"] = dark ? "B8BFC5" : "687079",
            ["PreviewBrush"] = dark ? "30363B" : "F3F4F5",
            ["TintBrush"] = dark ? "3C2D26" : "FFF2EB",
            ["PurpleTextBrush"] = dark ? "FFAA85" : "B84317",
            ["SelectedBrush"] = dark ? "533626" : "FFE6D9",
            ["WarningBrush"] = dark ? "F2C47B" : "986319"
        };
        foreach (var (name, value) in colors)
            Brush(name).Color = Color.FromArgb(255, Convert.ToByte(value[..2], 16), Convert.ToByte(value[2..4], 16), Convert.ToByte(value[4..6], 16));
    }
}
