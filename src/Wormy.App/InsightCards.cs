using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Data;
using Wormy.Core;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private Button InsightCard(string title, string size, string detail, string glyph, IEnumerable<FileEntry> examples)
    {
        var panel = new StackPanel { Spacing = 8 };
        var top = new Grid();
        top.Children.Add(new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = AppTheme.Brush("SelectedBrush"), HorizontalAlignment = HorizontalAlignment.Left, Child = new FontIcon { Glyph = glyph, FontSize = 16, Foreground = AppTheme.Brush("PurpleTextBrush") } });
        var previews = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var file in examples.Take(3))
        {
            var card = new FileCard(file);
            var image = new Image { Stretch = Stretch.UniformToFill, DataContext = card };
            image.SetBinding(Image.SourceProperty,new Binding { Path = new PropertyPath("Preview") });
            image.Loaded += async (_, _) => await card.LoadPreviewAsync();
            image.Unloaded += (_, _) => card.CancelPreview();
            var grid = new Grid();
            grid.Children.Add(new FontIcon { Glyph = card.Glyph, FontSize = 14, Foreground = AppTheme.Brush("MutedBrush") });
            grid.Children.Add(image);
            var frame = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = AppTheme.Brush("PreviewBrush"), BorderBrush = AppTheme.Brush("CardBrush"), BorderThickness = new Thickness(2), Margin = new Thickness(-7,0,0,0), Child = grid };
            ToolTipService.SetToolTip(frame,file.Name); previews.Children.Add(frame);
        }
        top.Children.Add(previews); panel.Children.Add(top);
        panel.Children.Add(new TextBlock { Text = title, FontSize = 12, Foreground = AppTheme.Brush("MutedBrush"), TextTrimming = TextTrimming.CharacterEllipsis });
        panel.Children.Add(new TextBlock { Text = size, FontSize = 23, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = detail + "  ›", FontSize = 11, Foreground = AppTheme.Brush("MutedBrush"), HorizontalAlignment = HorizontalAlignment.Right });
        return new Button { Content = panel, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(14), Margin = new Thickness(0,0,12,12), CornerRadius = new CornerRadius(16), Background = AppTheme.Brush("CardBrush"), BorderBrush = AppTheme.Brush("LineBrush"), BorderThickness = new Thickness(1) };
    }
}
