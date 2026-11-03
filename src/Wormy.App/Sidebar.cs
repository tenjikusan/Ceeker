using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Wormy.Core;

namespace Wormy.App;

public sealed partial class MainWindow
{
    private readonly Dictionary<string, TextBlock> sidebarCounts = new();
    private void InitializeSidebar()
    {
        if (sidebarCounts.Count > 0) return;
        if (HomeButton.Parent is not StackPanel panel) return;
        var existing = panel.Children.OfType<Button>().Where(b => b.Tag is string).ToDictionary(b => (string)b.Tag);
        foreach (var button in existing.Values) panel.Children.Remove(button);
        foreach (var heading in panel.Children.OfType<TextBlock>().ToArray()) panel.Children.Remove(heading);
        var order = new[] { ("Home", "Home", "\uE80F"), ("Recents", "Recents", "\uE823"), ("Images", "Images", "\uEB9F"), ("Videos", "Videos", "\uE714"), ("Music", "Music", "\uE8D6"), ("Documents", "Documents", "\uE8A5"), ("Archives", "Archives", "\uF012"), ("Apps", "Apps", "\uE71D"), ("Large files", "Large Files", "\uEDA2"), ("Screenshots", "Screenshots", "\uE722"), ("Duplicates", "Duplicates", "\uE8C8"), ("Cleanup review", "Clean Up", "\uEA99"), ("Downloaded from", "Downloaded From", "\uE896") };
        var position = 0;
        foreach (var (route, label, glyph) in order)
        {
            if (route == "Large files") panel.Children.Insert(position++, new Border { Height = 1, Background = AppTheme.Brush("LineBrush"), Margin = new Thickness(12,14,12,14) });
            if (!existing.Remove(route, out var button)) { button = new Button { Tag = route, Style = (Style)Shell.Resources["NavButton"] }; button.Click += Navigate; }
            button.MinHeight = 42;
            button.Padding = new Thickness(12,8,12,8);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 17 });
            var title = new TextBlock { Text = label, FontFamily = new FontFamily("Segoe UI"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(title,1); row.Children.Add(title);
            var count = new TextBlock { FontFamily = new FontFamily("Segoe UI"), FontSize = 12, Foreground = route is "Duplicates" or "Cleanup review" ? new SolidColorBrush(Windows.UI.Color.FromArgb(255,43,165,110)) : AppTheme.Brush("MutedBrush"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(count,2); row.Children.Add(count); sidebarCounts[route] = count;
            button.Content = row; panel.Children.Insert(position++,button);
            if (route == "Duplicates") ToolTipService.SetToolTip(button,"Duplicate groups from the latest scan; a dash means not scanned.");
            if (route == "Recents") ToolTipService.SetToolTip(button,"Files modified in the last 30 days.");
        }
        panel.Children.Insert(position++,new Border { Height = 1, Background = AppTheme.Brush("LineBrush"), Margin = new Thickness(12,14,12,14) });
        foreach (var button in existing.Values) panel.Children.Insert(position++,button);
        UpdateSidebarCounts();
    }
    private void UpdateSidebarCounts()
    {
        foreach (var (route, label) in sidebarCounts)
        {
            label.Text = route switch
            {
                "Home" or "Recents" => "",
                "Apps" => appSnapshot?.Apps.Count.ToString("N0") ?? "—",
                "Duplicates" => dashboardDuplicateGroups?.ToString("N0") ?? "—",
                "Cleanup review" => LibraryInsights.FormatBytes(CleanupSuggestions.Build(files,preferences.LargeFileMB*1_000_000L,preferences.DownloadAgeDays,preferences.ScreenshotAgeDays,DateTime.UtcNow).Sum(s=>(decimal)Math.Max(0,s.File.Length))),
                _ => files.Count(f=>FileBrowsing.InView(f,route,largeBytes:preferences.LargeFileMB*1_000_000L)).ToString("N0")
            };
        }
    }
}
