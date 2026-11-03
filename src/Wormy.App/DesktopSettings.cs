using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private void InitializeDesktopSettings()
    {
        var style=new ComboBox { Header="Visual style",HorizontalAlignment=HorizontalAlignment.Stretch };
        style.Items.Add("Playful · dimensional folders and warm accents");style.Items.Add("Minimal · outline icons and neutral panels");style.SelectedIndex=preferences.MinimalStyle?1:0;
        style.SelectionChanged+=(_,_)=>{preferences.MinimalStyle=style.SelectedIndex==1;SavePreferences();ApplyAppearance();};SettingsPanel.Children.Add(style);
        var global=new CheckBox { Content="Global Quick Search",IsChecked=preferences.GlobalQuickSearch };
        global.Click+=(_,_)=>{preferences.GlobalQuickSearch=global.IsChecked==true;SavePreferences();ConfigureDesktopIntegration();};SettingsPanel.Children.Add(global);
        var shortcut=new Button { Content="Change Quick Search shortcut…" };shortcut.Click+=async(_,_)=>await ChangeQuickShortcutAsync();SettingsPanel.Children.Add(shortcut);
        var quick=new Button { Content="Open Quick Search" };quick.Click+=(_,_)=>ShowQuickAccess(false);SettingsPanel.Children.Add(quick);
        var tray=new CheckBox { Content="Recent downloads in the Windows tray",IsChecked=preferences.TrayDownloads };
        tray.Click+=(_,_)=>{preferences.TrayDownloads=tray.IsChecked==true;SavePreferences();ConfigureDesktopIntegration();};SettingsPanel.Children.Add(tray);
        var recent=new Button { Content="Open recent downloads" };recent.Click+=(_,_)=>ShowQuickAccess(true);SettingsPanel.Children.Add(recent);
        SettingsPanel.Children.Add(new TextBlock { Text="Desktop shortcuts and tray stay active while Ceeker is running. Closing Ceeker exits them. Download ordering uses first seen by Ceeker.",FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=AppTheme.Brush("MutedBrush") });
    }
    private async Task ChangeQuickShortcutAsync()
    {
        uint key=preferences.QuickSearchKey,mods=preferences.QuickSearchModifiers;
        var capture=new TextBox { Header="Press Ctrl/Alt/Shift with a letter, number, Space or function key",Text="Click here and press your shortcut",IsReadOnly=true,TextWrapping=TextWrapping.Wrap };
        capture.KeyDown+=(_,e)=>
        {
            var number=(uint)e.Key;if(number<32||number>123)return;
            bool Down(VirtualKey k)=>(Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(k)&Windows.UI.Core.CoreVirtualKeyStates.Down)!=0;
            var value=(Down(VirtualKey.Menu)?1u:0u)|(Down(VirtualKey.Control)?2u:0u)|(Down(VirtualKey.Shift)?4u:0u);
            if(value==0)return;key=number;mods=value;capture.Text=$"{(value.HasFlagBits(2)?"Ctrl+":"")}{(value.HasFlagBits(1)?"Alt+":"")}{(value.HasFlagBits(4)?"Shift+":"")}{e.Key}";e.Handled=true;
        };
        var dialog=new ContentDialog { XamlRoot=Shell.XamlRoot,RequestedTheme=Shell.RequestedTheme,Title="Quick Search shortcut",Content=capture,PrimaryButtonText="Save",CloseButtonText="Cancel" };
        if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
        if(desktop is not null&&!desktop.RegisterShortcut(true,mods,key))
        {desktop.RegisterShortcut(preferences.GlobalQuickSearch,preferences.QuickSearchModifiers,preferences.QuickSearchKey);Status.Text="Shortcut already in use · Previous shortcut retained.";return;}
        preferences.QuickSearchKey=key;preferences.QuickSearchModifiers=mods;preferences.GlobalQuickSearch=true;SavePreferences();Status.Text="Global Quick Search shortcut saved.";
    }
    private void ToggleVisualStyle(object sender,RoutedEventArgs args){preferences.MinimalStyle=!preferences.MinimalStyle;SavePreferences();ApplyAppearance();}
    private void ApplyVisualStyle()
    {
        AppTheme.Minimal=preferences.MinimalStyle;
        var dark=Shell.RequestedTheme==ElementTheme.Dark;
        if(preferences.MinimalStyle)
        {
            AppTheme.Brush("TintBrush").Color=dark?Windows.UI.Color.FromArgb(255,47,50,53):Windows.UI.Color.FromArgb(255,238,239,240);
            AppTheme.Brush("SelectedBrush").Color=dark?Windows.UI.Color.FromArgb(255,67,72,77):Windows.UI.Color.FromArgb(255,220,223,226);
            AppTheme.Brush("PurpleTextBrush").Color=dark?Windows.UI.Color.FromArgb(255,239,240,241):Windows.UI.Color.FromArgb(255,48,52,56);
        }
        if(StyleToggleButton is not null){StyleToggleButton.Content=preferences.MinimalStyle?"Playful":"Minimal";}
        UpdateCategories();
    }
    private void InitializeCompactNavigation()
    {
        var menu=new MenuFlyout();CompactNavigationButton.Flyout=menu;
        menu.Opening+=(_,_)=>
        {
            menu.Items.Clear();foreach(var route in new[]{"Home","All files","Recents","Images","Videos","Music","Documents","Archives","Apps","Duplicates","Similar photos","Cleanup review","Storage","Downloads","Downloaded from"})
            {var item=new MenuFlyoutItem { Text=route };item.Click+=(_,_)=>NavigateTo(route);menu.Items.Add(item);}
            var saved=new MenuFlyoutSubItem { Text="Saved searches" };
            foreach(var search in preferences.SavedSearches){var item=new MenuFlyoutItem { Text=search.Name };item.Click+=(_,_)=>{history.ReplaceCurrent(CurrentRoute());var route=new BrowseRoute(search.Category,search.Folder,search.Query,search.Contents,search.Source,search.CurrentView);history.Visit(route);RestoreRoute(route);};saved.Items.Add(item);}if(saved.Items.Count>0)menu.Items.Add(saved);
            var locations=new MenuFlyoutSubItem { Text="Connected folders" };foreach(var root in roots){var item=new MenuFlyoutItem { Text=root.Name };item.Click+=(_,_)=>NavigateTo(root.Name,root.Path);locations.Items.Add(item);}if(locations.Items.Count>0)menu.Items.Add(locations);
        };
    }
}
internal static class ModifierBits { public static bool HasFlagBits(this uint value,uint flag)=>(value&flag)!=0; }
