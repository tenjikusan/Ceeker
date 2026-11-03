using Microsoft.UI.Xaml;
using System.Reflection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private void MoveSettingsToMenu()
    {
        var version=System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";
        SettingsPanel.Children.Add(new TextBlock { Text="Ceeker "+version,FontSize=12,Foreground=AppTheme.Brush("MutedBrush") });
        var groups = new Dictionary<UIElement,string>();
        void AddSettings(string section, Action initialize)
        {
            var count = SettingsPanel.Children.Count;
            initialize();
            foreach (var item in SettingsPanel.Children.Skip(count)) groups[item] = section;
        }
        AddSettings("Desktop & shortcuts", InitializeDesktopSettings);
        AddSettings("Folders & indexing", InitializeAutomaticRulesSettings);
        AddSettings("Search & OCR", InitializeOcrLanguages);
        AddSettings("Search & OCR", InitializeSemanticSearch);
        AddSettings("App usage & privacy", InitializeUsageObservations);
        AddSettings("Updates & licensing", InitializeSignedUpdates);
        AddSettings("Updates & licensing", InitializeLicenseSettings);
        AddSettings("Cleanup & review", InitializeInstallerEvidence);
        var systemTheme = new CheckBox { Content = "Follow Windows appearance", IsChecked = preferences.FollowSystemTheme };
        systemTheme.Click += (_, _) => { preferences.FollowSystemTheme = systemTheme.IsChecked == true; SavePreferences(); ApplyAppearance(); };
        SettingsPanel.Children.Add(systemTheme); groups[systemTheme] = "Appearance & touch";
        var related=new CheckBox { Content="Offline related-word search",IsChecked=preferences.RelatedWords };
        ToolTipService.SetToolTip(related,"Use a small local synonym list for names, tags and indexed text. This is not visual AI search.");
        related.Click+=(_,_)=> { preferences.RelatedWords=related.IsChecked==true; SavePreferences(); UpdateFiles(); };
        SettingsPanel.Children.Add(related); groups[related] = "Search & OCR";
        if (HomeButton.Parent is StackPanel navigation)
            foreach (var control in navigation.Children.Where(c => c is CheckBox || c is Button { Tag: null }).ToArray())
            {
                navigation.Children.Remove(control); SettingsPanel.Children.Add(control);
                groups[control] = control == touchChoice ? "Appearance & touch"
                    : control == automaticDuplicatesChoice ? "Cleanup & review"
                    : control is Button { Content: string label } ? label switch
                    {
                        "Review settings…" => "Cleanup & review",
                        "Scanning settings…" or "Suggested folders…" => "Folders & indexing",
                        "Updates…" => "Updates & licensing",
                        _ => "Help"
                    } : "Folders & indexing";
            }
        DependencyObject? owner = RootPicker;
        while (owner is not null && owner is not StackPanel) owner = VisualTreeHelper.GetParent(owner);
        if (owner is StackPanel locations)
            foreach (var control in locations.Children.Where(c => c is Grid || c is CheckBox).ToArray())
            { locations.Children.Remove(control); SettingsPanel.Children.Add(control); groups[control] = "Folders & indexing"; }
        groups[themeChoice] = "Appearance & touch";
        groups[backgroundOcrChoice] = "Search & OCR";
        foreach (var control in SettingsPanel.Children.OfType<Button>())
        {
            control.Style = null;
            control.FontSize = 14;
            control.FontFamily=new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI");
            control.CornerRadius=new CornerRadius(6);
            control.HorizontalContentAlignment=HorizontalAlignment.Left;
            control.HorizontalAlignment = HorizontalAlignment.Stretch;
            control.MinHeight = 40;
            control.Padding = new Thickness(12,8,12,8);
            if(control.Content is string label) control.Content = new TextBlock { Text=label, TextWrapping=TextWrapping.Wrap };
            control.Click += (_, _) => SettingsFlyout.Hide();
        }
        foreach(var control in SettingsPanel.Children.OfType<CheckBox>().ToArray())
        {
            control.IsThreeState=false;
            control.FontFamily=new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI");
            control.HorizontalAlignment=HorizontalAlignment.Stretch;
            control.MinHeight=40;
            if(control.Content is string label) control.Content=new TextBlock { Text=label,TextWrapping=TextWrapping.Wrap,MaxWidth=264 };
            var position=SettingsPanel.Children.IndexOf(control);SettingsPanel.Children.Remove(control);
            var row = new Border { Child=control,CornerRadius=new CornerRadius(6),Padding=new Thickness(12,6,12,6),Background=AppTheme.Brush("CardBrush"),BorderBrush=AppTheme.Brush("LineBrush"),BorderThickness=new Thickness(1) };
            if (groups.Remove(control, out var section)) groups[row] = section;
            SettingsPanel.Children.Insert(position,row);
        }
        var entries = SettingsPanel.Children.ToArray();
        SettingsPanel.Children.Clear();
        foreach (var entry in entries.Where(item => !groups.ContainsKey(item))) SettingsPanel.Children.Add(entry);
        foreach (var section in new[] { "Desktop & shortcuts", "Search & OCR", "Appearance & touch", "Folders & indexing", "Cleanup & review", "App usage & privacy", "Updates & licensing", "Help" })
        {
            var items = entries.Where(item => groups.TryGetValue(item, out var group) && group == section).ToArray();
            if (items.Length == 0) continue;
            var panel = new StackPanel { Spacing = 8, Margin = new Thickness(0,12,0,0) };
            panel.Children.Add(new TextBlock { Text=section, FontSize=13, FontWeight=Microsoft.UI.Text.FontWeights.SemiBold, Foreground=AppTheme.Brush("MutedBrush"), Margin=new Thickness(0,0,0,2) });
            foreach (var item in items) panel.Children.Add(item);
            SettingsPanel.Children.Add(panel);
        }
    }
}
