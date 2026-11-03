using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wormy.Core;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private readonly StackPanel folderChildren=new() { Spacing=8,Visibility=Visibility.Collapsed };
    private int folderInventoryRequest;
    private void InitializeFolderHierarchy()
    {
        Shell.Loaded += (_,_)=> { if(FileSection.Parent is StackPanel panel) panel.Children.Insert(panel.Children.IndexOf(FileSection),folderChildren); };
    }
    private async void UpdateFolderHierarchy()
    {
        var request=++folderInventoryRequest;
        folderChildren.Children.Clear();
        folderChildren.Visibility=Visibility.Collapsed;
        if(pinnedFolder is null || !string.IsNullOrWhiteSpace(SearchBox.Text)) return;
        var folder=pinnedFolder;
        var prefix=System.IO.Path.TrimEndingDirectorySeparator(pinnedFolder)+System.IO.Path.DirectorySeparatorChar;
        var groups=files.Where(f=>FileBrowsing.InFolder(f,folder)).Select(f=> (File:f,Relative:f.Path[prefix.Length..])).Where(item=>item.Relative.Contains(System.IO.Path.DirectorySeparatorChar)).GroupBy(item=>item.Relative.Split(System.IO.Path.DirectorySeparatorChar)[0],StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Select(item=>item.File).ToArray(),StringComparer.OrdinalIgnoreCase);
        FolderInventoryResult inventory;
        try { var options=CurrentScanOptions(); inventory=await Task.Run(()=>FolderInventory.Read(folder,options,CancellationToken.None)); }
        catch(Exception ex) { if(request==folderInventoryRequest) { folderChildren.Children.Add(new TextBlock { Text="Folder inventory unavailable: "+ex.Message,TextWrapping=TextWrapping.Wrap }); folderChildren.Visibility=Visibility.Visible; } return; }
        if(request!=folderInventoryRequest || closed) return;
        var allocation=new Button { Content="Measure folder file allocation",HorizontalAlignment=HorizontalAlignment.Left };
        var allocationText=new TextBlock { TextWrapping=TextWrapping.Wrap,FontSize=11 };
        var allocationFiles=files.Where(f=>FileBrowsing.InFolder(f,folder)).ToArray();
        allocation.Click+=async(_,_)=>
        {
            allocation.IsEnabled=false; allocationText.Text="Measuring indexed files…";
            using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                var result=await Task.Run(()=>FolderAllocation.Measure(allocationFiles,cancellation.Token),cancellation.Token);
                allocationText.Text=$"{result.Measured:N0} files measured · {LibraryInsights.FormatBytes(result.LogicalBytes)} logical · {LibraryInsights.FormatBytes(result.AllocatedBytes)} allocated · {result.Unavailable:N0} unavailable · {result.NotProcessed:N0} beyond limit. Hard links are deduplicated when Windows supplies file IDs; directory records and unindexed files are excluded.";
            }
            catch(OperationCanceledException) { allocationText.Text="Measurement timed out. No files changed."; }
            catch(Exception ex) { allocationText.Text="Measurement unavailable: "+ex.Message; }
            finally { allocation.IsEnabled=true; }
        };
        folderChildren.Children.Add(allocation); folderChildren.Children.Add(allocationText);
        var root=roots.FirstOrDefault(r=>string.Equals(r.Path,pinnedFolder,StringComparison.OrdinalIgnoreCase)||FileBrowsing.InFolder(new FileEntry(pinnedFolder,"",0,DateTime.MinValue),r.Path));
        if(root is not null && !string.Equals(root.Path,pinnedFolder,StringComparison.OrdinalIgnoreCase))
        {
            var up=new Button { Content="↑ Parent folder" }; var parent=System.IO.Path.GetDirectoryName(pinnedFolder)!;
            up.Click+=(_,_)=>NavigateTo(System.IO.Path.GetFileName(parent),parent,clearSearch:true); folderChildren.Children.Add(up);
        }
        if(inventory.Folders.Count > 0)
        {
            folderChildren.Children.Add(new TextBlock { Text="Folders",FontWeight=Microsoft.UI.Text.FontWeights.SemiBold });
            var shelf=new VariableSizedWrapGrid { Orientation=Orientation.Horizontal,ItemWidth=220,ItemHeight=88,MaximumRowsOrColumns=5 };
            foreach(var item in inventory.Folders)
            {
                var path=item.Path;
                var indexed=groups.TryGetValue(item.Name,out var entries) ? entries : [];
                var content=new StackPanel { Spacing=5 };
                content.Children.Add(new TextBlock { Text="📁 " + item.Name,TextTrimming=TextTrimming.CharacterEllipsis });
                content.Children.Add(new TextBlock { Text=item.Empty ? "Empty folder" : $"{indexed.Length:N0} indexed files · {LibraryInsights.FormatBytes(indexed.Sum(file=>(decimal)file.Length))}",FontSize=11 });
                var button=new Button { Content=content,HorizontalContentAlignment=HorizontalAlignment.Stretch,HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,0,10,10) };
                ToolTipService.SetToolTip(button,"Indexed files in this folder and its subfolders · logical sizes");
                button.Click+=(_,_)=>NavigateTo(item.Name,path,clearSearch:true); shelf.Children.Add(button);
            }
            folderChildren.Children.Add(shelf);
        }
        if(inventory.Unavailable>0 || inventory.Limited) folderChildren.Children.Add(new TextBlock { Text=$"{inventory.Unavailable} folders unavailable"+(inventory.Limited ? " · Showing first 1,000 folders" : ""),FontSize=11 });
        folderChildren.Visibility=folderChildren.Children.Count==0 ? Visibility.Collapsed : Visibility.Visible;
    }
}
