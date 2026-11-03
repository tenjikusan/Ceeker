using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wormy.Core;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private void RenderSavedSearches()
    {
        SavedSearchPanel.Children.Clear();
        if(preferences.SavedSearches.Count==0)return;
        SavedSearchPanel.Children.Add(new TextBlock { Text="SAVED SEARCHES",FontSize=11,Margin=new Thickness(12,16,0,4),Foreground=AppTheme.Brush("MutedBrush") });
        foreach(var search in preferences.SavedSearches)
        {
            var button=new Button { Content=search.Name,Style=(Style)Shell.Resources["NavButton"] };
            ToolTipService.SetToolTip(button,search.Query);
            button.Click+=(_,_)=>
            {
                if(previewOpen)return;
                history.ReplaceCurrent(CurrentRoute());
                var route=new BrowseRoute(search.Category,search.Folder,search.Query,search.Contents,search.Source,search.CurrentView);
                history.Visit(route);RestoreRoute(route,button);
            };
            var menu=new MenuFlyout();var rename=new MenuFlyoutItem { Text="Rename…" };var delete=new MenuFlyoutItem { Text="Remove saved search" };
            rename.Click+=async(_,_)=>
            {
                var name=new TextBox { Text=search.Name,MaxLength=64,Header="Name" };
                var dialog=new ContentDialog { XamlRoot=Shell.XamlRoot,RequestedTheme=Shell.RequestedTheme,Title="Rename saved search",Content=name,PrimaryButtonText="Save",CloseButtonText="Cancel" };
                if(await dialog.ShowAsync()!=ContentDialogResult.Primary||string.IsNullOrWhiteSpace(name.Text))return;
                var at=preferences.SavedSearches.FindIndex(s=>s.Id==search.Id);if(at<0)return;preferences.SavedSearches[at]=search with { Name=name.Text.Trim() };SavePreferences();RenderSavedSearches();
            };
            delete.Click+=(_,_)=> { preferences.SavedSearches.RemoveAll(s=>s.Id==search.Id);SavePreferences();RenderSavedSearches(); };
            menu.Items.Add(rename);menu.Items.Add(delete);button.ContextFlyout=menu;SavedSearchPanel.Children.Add(button);
        }
    }
    private async void SaveCurrentSearch(object sender,RoutedEventArgs args)
    {
        if(previewOpen||string.IsNullOrWhiteSpace(SearchBox.Text))return;
        if(preferences.SavedSearches.Count>=50){Status.Text="50 saved searches reached · Remove one before adding another.";return;}
        var name=new TextBox { Header="Name",Text=SearchBox.Text[..Math.Min(64,SearchBox.Text.Length)],MaxLength=64 };
        var dialog=new ContentDialog { XamlRoot=Shell.XamlRoot,RequestedTheme=Shell.RequestedTheme,Title="Save live search",Content=name,PrimaryButtonText="Save",CloseButtonText="Cancel" };
        if(await dialog.ShowAsync()!=ContentDialogResult.Primary||string.IsNullOrWhiteSpace(name.Text))return;
        var route=CurrentRoute();preferences.SavedSearches.Add(new(Guid.NewGuid().ToString("N"),name.Text.Trim(),route.Query,route.Category,route.Folder,route.Source,route.Contents,route.CurrentView));
        SavePreferences();RenderSavedSearches();Status.Text="Saved search added · Relative dates are evaluated whenever you open it.";
    }
}
