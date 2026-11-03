using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.System;
using Wormy.Core;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private DesktopNative? desktop;
    private QuickAccessWindow? quickAccess;
    private string[]? pendingCommand;
    private void InitializeDesktopIntegration()
    {
        try
        {
            desktop=new(WinRT.Interop.WindowNative.GetWindowHandle(this),Path.Combine(AppContext.BaseDirectory,"Assets","ceeker.ico"),()=>DispatcherQueue.TryEnqueue(()=>ShowQuickAccess(false)),()=>DispatcherQueue.TryEnqueue(()=>ShowQuickAccess(true)));
            ConfigureDesktopIntegration();
        }
        catch(Exception ex){Status.Text="Desktop integration unavailable: "+ex.Message;}
        Closed+=(_,_)=>{desktop?.Dispose();quickAccess?.Close();};
        AddShortcut(VirtualKey.Add,VirtualKeyModifiers.Control,()=>ChangeCardSize(20));
        AddShortcut(VirtualKey.Subtract,VirtualKeyModifiers.Control,()=>ChangeCardSize(-20));
        AddShortcut((VirtualKey)187,VirtualKeyModifiers.Control,()=>ChangeCardSize(20));
        AddShortcut((VirtualKey)189,VirtualKeyModifiers.Control,()=>ChangeCardSize(-20));
        AddShortcut(VirtualKey.T,VirtualKeyModifiers.Control|VirtualKeyModifiers.Shift,()=>{preferences.MinimalStyle=!preferences.MinimalStyle;SavePreferences();ApplyAppearance();});
        InitializeCompactNavigation();
        Shell.SizeChanged+=(_,_)=>ApplyResponsiveLayout();ApplyResponsiveLayout();RenderSavedSearches();
    }
    private void ConfigureDesktopIntegration()
    {
        if(desktop is null)return;
        if(!desktop.RegisterShortcut(preferences.GlobalQuickSearch,preferences.QuickSearchModifiers,preferences.QuickSearchKey))Status.Text="Quick Search shortcut is in use · Choose another shortcut in Settings.";
        if(!desktop.SetTray(preferences.TrayDownloads))Status.Text="Tray icon unavailable · Quick Search remains accessible from Settings.";
    }
    private void ShowQuickAccess(bool recent)
    {
        if(closed||!loaded)return;
        if(previewOpen){Activate();return;}
        if(quickAccess is null)
        {
            quickAccess=new((DataTemplate)Shell.Resources["FileRow"],Shell.RequestedTheme,QuickSearchFilesAsync,QuickAction,command=>
            {
                if(command=="search"){ShowQuickAccess(false);return;}if(command=="exit"){Close();return;}Activate();
                if(command=="settings")SettingsButton.Flyout.ShowAt(SettingsButton);
            });
            quickAccess.Closed+=(_,_)=>quickAccess=null;
        }
        var area=DisplayArea.GetFromWindowId(AppWindow.Id,DisplayAreaFallback.Nearest).WorkArea;
        quickAccess.Show(recent,recent?DesktopFeatures.RecentDownloads(files):files.OrderByDescending(f=>f.ModifiedUtc).Take(20).ToArray(),area,Shell.RequestedTheme);
    }
    private async Task<FileEntry[]> QuickSearchFilesAsync(string text,CancellationToken token)
    {
        var snapshot=files.ToArray();if(string.IsNullOrWhiteSpace(text))return snapshot.OrderByDescending(f=>f.ModifiedUtc).Take(20).ToArray();
        var query=SearchParser.Parse(text);var related=preferences.RelatedWords&&!text.Contains('"');
        var enriched=await DeviceSearchSnapshotAsync(snapshot,query,token);
        var content=preferences.IncludeContents&&query.Terms.Count>0?await index.SearchContentAsync(query.Terms,token,related):null;
        var tags=await index.LoadTagsAsync(token);
        var semantic=await SemanticMatchesAsync(query,preferences.SemanticSearch&&!text.Contains('"')?enriched:[],token);
        return await Task.Run(()=>enriched.Where(f=>FileSearch.Matches(query,f,tags.GetValueOrDefault(f.Path)??[],content?.Contains(f.Path)==true,related)||semantic.ContainsKey(f.Path)).OrderByDescending(f=>semantic.GetValueOrDefault(f.Path)).ThenByDescending(f=>f.ModifiedUtc).Take(100).ToArray(),token);
    }
    private async void QuickAction(FileEntry file,string action)
    {
        try
        {
            if(action=="open"){Status.Text=await FileActions.OpenAsync(file.Path);return;}
            if(action=="explorer"){Status.Text=FileActions.Reveal(file.Path);return;}
            if(action=="copy"){Status.Text=FileActions.CopyPath(file.Path);return;}
            Activate();if(action=="preview"){await ShowPreview(file,quickAccess?.Snapshot);return;}
            NavigateTo("All files",Path.GetDirectoryName(file.Path));SearchCurrentView.IsChecked=true;SearchBox.Text='"'+file.Name.Replace("\"","")+'"';
        }
        catch(Exception ex){Status.Text="Unable to use file: "+ex.Message;}
    }
    public void QueueExternalCommand(string[] arguments)
    {
        pendingCommand=arguments;if(loaded)ApplyExternalCommand();
    }
    private void ApplyExternalCommand()
    {
        if(pendingCommand is not { } args)return;pendingCommand=null;
        if(Array.IndexOf(args,"--quick-search")>=0){ShowQuickAccess(false);return;}
        if(Array.IndexOf(args,"--latest-download")>=0){ShowQuickAccess(true);return;}
        var at=Array.IndexOf(args,"--search");if(at>=0&&at+1<args.Length){NavigateTo("All files");SearchCurrentView.IsChecked=false;SearchBox.Text=args[at+1][..Math.Min(1000,args[at+1].Length)];Activate();}
    }
    private void ChangeCardSize(int delta)=>ThumbnailSize.Value=Math.Clamp(ThumbnailSize.Value+delta,ThumbnailSize.Minimum,ThumbnailSize.Maximum);
    private void ApplyResponsiveLayout()
    {
        if(Shell.ActualWidth<=0)return;var narrow=Shell.ActualWidth<1050;var small=Shell.ActualWidth<760;
        SidebarColumn.Width=new GridLength(small?0:narrow?200:260); SidebarBorder.Visibility=small?Visibility.Collapsed:Visibility.Visible; CompactNavigationButton.Visibility=small?Visibility.Visible:Visibility.Collapsed;HeaderLibrarySize.Visibility=narrow?Visibility.Collapsed:Visibility.Visible;
        SidebarBorder.Padding=new Thickness(small?2:0);TopBar.Padding=new Thickness(narrow?12:24,12,12,12);MainContent.Padding=new Thickness(narrow?12:28,16,12,16);
        Grid.SetRow(SearchBox,narrow?1:0);Grid.SetColumnSpan(SearchBox,narrow?5:1);Grid.SetColumn(SearchBox,0);
        Grid.SetColumn(ConnectFolderButton,small?1:narrow?0:2);ConnectFolderButton.HorizontalAlignment=narrow?HorizontalAlignment.Left:HorizontalAlignment.Stretch;
        LocationLinks.MaxHeight=Shell.ActualHeight<700?100:220;BottomDashboard.MaxHeight=Shell.ActualHeight<700?180:300;
        {var available=Math.Max(160,Shell.ActualWidth-SidebarColumn.Width.Value-70);var tile=narrow?Math.Min(preferences.TileWidth,Math.Max(160,available/2-16)):preferences.TileWidth;foreach(var card in FileGrid.Items.OfType<FileCard>())card.Resize(tile);}
    }
}

