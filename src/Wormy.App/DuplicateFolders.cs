using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Wormy.Core;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private string[]? duplicateFolders;
    private bool choosingDuplicateFolders;
    private async void ChooseDuplicateFolders(object sender, RoutedEventArgs args)
    {
        if (busy || previewOpen) return;
        var choices = new Dictionary<string,CheckBox>(StringComparer.OrdinalIgnoreCase);
        var panel = new StackPanel { Spacing=10 };
        var all = new CheckBox { Content="All connected folders",IsChecked=duplicateFolders is null };
        panel.Children.Add(all);
        panel.Children.Add(new TextBlock { Text="Choose one or more connected folders, or a subfolder inside them. Includes indexed files in their subfolders; compares across every selected folder.",TextWrapping=TextWrapping.Wrap });
        var rows = new StackPanel { Spacing=8 }; panel.Children.Add(rows);
        void AddFolder(string path)
        {
            if (choices.ContainsKey(path)) return;
            var check = new CheckBox { Content=new TextBlock { Text=Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length:>0 } name ? name : path, TextWrapping=TextWrapping.Wrap }, IsChecked=duplicateFolders?.Contains(path,StringComparer.OrdinalIgnoreCase)==true,IsEnabled=all.IsChecked!=true };
            ToolTipService.SetToolTip(check,path);choices.Add(path,check);rows.Children.Add(check);
        }
        foreach(var root in roots) AddFolder(root.Path);
        foreach(var path in duplicateFolders ?? []) AddFolder(path);
        all.Click+=(_,_)=>{foreach(var check in choices.Values)check.IsEnabled=all.IsChecked!=true;};
        var message = new TextBlock { TextWrapping=TextWrapping.Wrap,Foreground=AppTheme.Brush("MutedBrush") };
        var browse = new Button { Content="Add a subfolder…" };
        browse.Click += async (_,_) =>
        {
            try
            {
                var picker=new FolderPicker();picker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
                var folder=await picker.PickSingleFolderAsync();if(folder is null)return;
                var path=Path.GetFullPath(folder.Path);
                if(!roots.Any(root=>string.Equals(Path.TrimEndingDirectorySeparator(root.Path),Path.TrimEndingDirectorySeparator(path),StringComparison.OrdinalIgnoreCase)||FileBrowsing.InFolder(new FileEntry(path,"",0,DateTime.MinValue),root.Path)))
                {message.Text="Connect this folder to the library first, then select it here.";return;}
                all.IsChecked=false;AddFolder(path);foreach(var check in choices.Values)check.IsEnabled=true;choices[path].IsChecked=true;message.Text="Subfolder selected.";
            }
            catch(Exception ex){message.Text="Unable to choose folder: "+ex.Message;}
        };
        panel.Children.Add(browse);panel.Children.Add(message);
        var dialog=new ContentDialog { XamlRoot=Shell.XamlRoot,RequestedTheme=Shell.RequestedTheme,Title="Duplicate scan folders",Content=new ScrollViewer { Content=panel,MaxHeight=380,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled },PrimaryButtonText="Apply",CloseButtonText="Cancel" };
        ContentDialogResult result; choosingDuplicateFolders=true;
        try { result=await dialog.ShowAsync(); } finally { choosingDuplicateFolders=false; }
        if(result!=ContentDialogResult.Primary)return;
        if(all.IsChecked!=true && !choices.Any(pair=>pair.Value.IsChecked==true)){Status.Text="Select at least one folder for duplicate scanning.";return;}
        duplicateFolders=all.IsChecked==true?null:choices.Where(pair=>pair.Value.IsChecked==true).Select(pair=>pair.Key).ToArray();
        ClearDuplicates();DuplicateScopeText.Text=DescribeDuplicateScope(duplicateFolders);
        DuplicateSummary.Text="Scan folders changed · Press Scan.";
    }
    private static string DescribeDuplicateScope(string[]? folders)=>folders is null ? "Scan scope: All connected folders" : "Scan scope: "+string.Join(" · ",folders.Select(path=>Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length:>0 } name?name:path));
}
