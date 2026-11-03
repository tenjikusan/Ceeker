using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Wormy.Core;
namespace Wormy.App;
internal sealed class QuickAccessWindow : Window
{
    private readonly TextBox search=new(){PlaceholderText="Search files, folders or indexed text…",Margin=new Thickness(0,8,0,8)};
    private readonly ListView results=new(){SelectionMode=ListViewSelectionMode.Extended,IsItemClickEnabled=false,CanDragItems=true};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,FontSize=12};
    private readonly Func<string,CancellationToken,Task<FileEntry[]>> find;
    private readonly Action<FileEntry,string> action;
    private CancellationTokenSource? pending;
    private FileCard[] cards=[];
    public FileEntry[] Snapshot => cards.Select(c=>c.Entry).ToArray();
    private bool recentMode,isClosed;
    public QuickAccessWindow(DataTemplate template,ElementTheme theme,Func<string,CancellationToken,Task<FileEntry[]>> searchFiles,Action<FileEntry,string> perform,Action<string> toolbar)
    {
        find=searchFiles;action=perform;Title="Ceeker Quick Search";
        var panel=new Grid { Padding=new Thickness(18),RowSpacing=8,Background=AppTheme.Brush("PageBrush"),RequestedTheme=theme };
        for(var i=0;i<4;i++)panel.RowDefinitions.Add(new RowDefinition{Height=i==2?new GridLength(1,GridUnitType.Star):GridLength.Auto});
        var commands=new VariableSizedWrapGrid { Orientation=Orientation.Horizontal,ItemWidth=180,ItemHeight=48,MaximumRowsOrColumns=4 };
        foreach(var (label,command) in new[]{("Quick Search","search"),("Open Ceeker","open"),("Settings","settings"),("Exit","exit")})
        {var button=new Button { Content=label };button.Click+=(_,_)=>{AppWindow.Hide();toolbar(command);};commands.Children.Add(button);}
        panel.Children.Add(commands);Grid.SetRow(search,1);panel.Children.Add(search);results.ItemTemplate=template;Grid.SetRow(results,2);panel.Children.Add(results);Grid.SetRow(status,3);panel.Children.Add(status);Content=panel;
        search.TextChanged+=async(_,_)=>await UpdateAsync();
        search.KeyDown+=(_,e)=>{if(e.Key==VirtualKey.Down&&results.Items.Count>0){results.SelectedIndex=0;results.Focus(FocusState.Keyboard);e.Handled=true;}};
        panel.AddHandler(UIElement.KeyDownEvent,new KeyEventHandler((_,e)=>
        {
            if(e.Key==VirtualKey.Escape){pending?.Cancel();AppWindow.Hide();e.Handled=true;return;}
            if(e.Key!=VirtualKey.Enter || results.SelectedItem is not FileCard card)return;
            var alt=(Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)&Windows.UI.Core.CoreVirtualKeyStates.Down)!=0;
            var ctrl=(Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)&Windows.UI.Core.CoreVirtualKeyStates.Down)!=0;
            AppWindow.Hide();action(card.Entry,alt?"explorer":ctrl?"ceeker":"open");e.Handled=true;
        }),true);
        results.DoubleTapped+=(_,_)=>{if(results.SelectedItem is FileCard card){AppWindow.Hide();action(card.Entry,"open");}};
        results.RightTapped+=(_,_)=>
        {
            if(results.SelectedItem is not FileCard card)return;
            var menu=new MenuFlyout();foreach(var (label,command) in new[]{("Open","open"),("Preview","preview"),("Show in Ceeker","ceeker"),("Show in Explorer","explorer"),("Copy path","copy")})
            {var item=new MenuFlyoutItem { Text=label };item.Click+=(_,_)=>{AppWindow.Hide();action(card.Entry,command);};menu.Items.Add(item);}menu.ShowAt(results);
        };
        results.DragItemsStarting+=(_,e)=>{var entries=e.Items.OfType<FileCard>().Select(c=>c.Entry).ToArray();if(entries.Length==0){e.Cancel=true;return;}FileTransfer.Configure(e.Data,entries,ThumbnailCache.Validate,error=>DispatcherQueue.TryEnqueue(()=>status.Text=error));};
        results.ContainerContentChanging+=async(_,e)=>{if(e.Item is FileCard card&&!e.InRecycleQueue)await card.LoadPreviewAsync();};
        Activated+=(_,e)=>{if(e.WindowActivationState==WindowActivationState.Deactivated){pending?.Cancel();AppWindow.Hide();}};
        Closed+=(_,_)=>{isClosed=true;pending?.Cancel();foreach(var card in cards)card.CancelPreview();};
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory,"Assets","ceeker.ico"));AppWindow.Resize(new Windows.Graphics.SizeInt32(820,700));
    }
    public void Show(bool downloads,FileEntry[] recent,Windows.Graphics.RectInt32 work,ElementTheme theme)
    {
        if(isClosed)return;if(Content is FrameworkElement surface)surface.RequestedTheme=theme;var width=Math.Min(820,work.Width);var height=Math.Min(700,work.Height);AppWindow.Resize(new Windows.Graphics.SizeInt32(width,height));recentMode=downloads;search.Visibility=downloads?Visibility.Collapsed:Visibility.Visible;Title=downloads?"Ceeker · Recent downloads":"Ceeker · Quick Search";
        AppWindow.Move(new Windows.Graphics.PointInt32(downloads?Math.Max(work.X,work.X+work.Width-width-20):work.X+Math.Max(0,(work.Width-width)/2),downloads?Math.Max(work.Y,work.Y+work.Height-height-20):work.Y+Math.Min(60,Math.Max(0,work.Height-height))));
        pending?.Cancel();SetResults(recent);status.Text=downloads?"10 most recent downloads · First seen by Ceeker, not the browser download timestamp. Drag one or several into another app.":"Recent files · Enter opens · Ctrl+Enter shows in Ceeker · Alt+Enter shows in Explorer";
        search.Text="";Activate();if(!downloads)DispatcherQueue.TryEnqueue(()=>search.Focus(FocusState.Keyboard));
    }
    private async Task UpdateAsync()
    {
        if(recentMode||isClosed)return;
        pending?.Cancel();var cancellation=new CancellationTokenSource();pending=cancellation;
        try
        {
            status.Text="Searching locally…";await Task.Delay(180,cancellation.Token);
            var files=await find(search.Text,cancellation.Token);cancellation.Token.ThrowIfCancellationRequested();if(isClosed)return;
            SetResults(files);status.Text=$"{files.Length} results shown · Enter opens · Ctrl+Enter: Ceeker · Alt+Enter: Explorer";
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(!isClosed&&pending==cancellation)status.Text="Search unavailable: "+ex.Message;}
        finally{if(pending==cancellation)pending=null;cancellation.Dispose();}
    }
    private void SetResults(FileEntry[] files)
    {
        foreach(var card in cards)card.CancelPreview();cards=files.Select(f=>new FileCard(f,180)).ToArray();results.ItemsSource=cards;if(cards.Length>0)results.SelectedIndex=0;
    }
}

