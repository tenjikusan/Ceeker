using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wormy.Core;
using Wormy.Infrastructure;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private async void DisconnectDuplicateFolder(object sender,RoutedEventArgs args)
    {
        if(busy||previewOpen)return;
        if(roots.Count==0){Status.Text="No connected folders to disconnect.";return;}
        busy=true;
        try
        {
            var picker=new ComboBox { ItemsSource=roots,DisplayMemberPath="Name",SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch };
            var path=new TextBlock { TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true };
            void ShowPath()=>path.Text=(picker.SelectedItem as IndexedRoot)?.Path ?? "";
            picker.SelectionChanged+=(_,_)=>ShowPath();ShowPath();
            var panel=new StackPanel { Spacing=12 };panel.Children.Add(new TextBlock { Text="Disconnect a folder from Ceeker's library. Its original files remain on disk. Other connected folders are retained.",TextWrapping=TextWrapping.Wrap });panel.Children.Add(picker);panel.Children.Add(path);
            var dialog=new ContentDialog { XamlRoot=Shell.XamlRoot,RequestedTheme=Shell.RequestedTheme,Title="Disconnect folder?",Content=panel,PrimaryButtonText="Disconnect",CloseButtonText="Cancel",DefaultButton=ContentDialogButton.Close };
            if(await dialog.ShowAsync()!=ContentDialogResult.Primary||picker.SelectedItem is not IndexedRoot root)return;
            await index.RemoveRootAsync(root.Path);await ReloadSnapshot();
            if(duplicateFolders is not null)duplicateFolders=duplicateFolders.Where(folder=>roots.Any(connected=>string.Equals(folder,connected.Path,StringComparison.OrdinalIgnoreCase)||FileBrowsing.InFolder(new FileEntry(folder,"",0,DateTime.MinValue),connected.Path))).ToArray();
            Status.Text="Folder disconnected · Original files retained";
        }
        catch(Exception ex){Status.Text="Unable to disconnect folder: "+ex.Message;}
        finally{busy=false;}
    }
}
