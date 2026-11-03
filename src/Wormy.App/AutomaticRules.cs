using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wormy.Core;
using Wormy.Infrastructure;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private DateTime nextRuleRun=DateTime.UtcNow.AddMinutes(5);
    private void InitializeAutomaticRulesSettings()
    {
        var button=new Button { Content="Automatic cleanup rules…" };button.Click+=async(_,_)=>await ShowAutomaticRulesAsync();SettingsPanel.Children.Add(button);
        var log=new Button { Content="View cleanup-rule history" };log.Click+=async(_,_)=>
        {
            var path=preferencesPath+".rules.jsonl";var text=File.Exists(path)?string.Join("\n",File.ReadLines(path).TakeLast(30)):"No automatic actions recorded.";
            await new ContentDialog { XamlRoot=Shell.XamlRoot,RequestedTheme=Shell.RequestedTheme,Title="Automatic rule history",Content=new ScrollViewer { Content=new TextBlock { Text=text,TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true },MaxHeight=380 },CloseButtonText="Close" }.ShowAsync();
        };SettingsPanel.Children.Add(log);
        automaticTimer.Tick+=AutomaticRulesTick;
    }
    private CleanupRuleOptions CurrentRules()=>new(preferences.RuleFolder,preferences.RuleRecycleInstallers,preferences.RuleOrganizeScreenshots,preferences.RuleInstallerAgeDays);
    private async Task ShowAutomaticRulesAsync()
    {
        var folder=new ComboBox { Header="Apply only inside this connected folder",ItemsSource=roots,DisplayMemberPath="Name",HorizontalAlignment=HorizontalAlignment.Stretch,PlaceholderText="Choose a folder" };
        folder.SelectedItem=roots.FirstOrDefault(r=>string.Equals(r.Path,preferences.RuleFolder,StringComparison.OrdinalIgnoreCase));
        var recycle=new CheckBox { Content="Move old installer files to Windows Recycle Bin",IsChecked=preferences.RuleRecycleInstallers };
        var screenshots=new CheckBox { Content="File screenshots into Ceeker Screenshots / YYYY-MM",IsChecked=preferences.RuleOrganizeScreenshots };
        var age=new NumberBox { Header="Installer minimum age (days)",Minimum=30,Maximum=3650,Value=preferences.RuleInstallerAgeDays,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact };
        var preview=new TextBlock { TextWrapping=TextWrapping.Wrap,FontSize=12 };
        var panel=new StackPanel { Spacing=12 };panel.Children.Add(new TextBlock { Text="Off by default. Enabling a rule authorizes automatic actions while Ceeker is running. Only indexed files in your chosen folder are eligible. Installer age does not prove it is installed or unneeded. Each run handles at most 10 files, never overwrites a destination, and records actions locally. Original screenshots are moved, not copied. Recycle Bin can restore installers until it is emptied.",TextWrapping=TextWrapping.Wrap });
        panel.Children.Add(folder);panel.Children.Add(recycle);panel.Children.Add(age);panel.Children.Add(screenshots);
        var check=new Button { Content="Preview eligible actions" };check.Click+=(_,_)=>
        {
            var rule=new CleanupRuleOptions((folder.SelectedItem as IndexedRoot)?.Path??"",recycle.IsChecked==true,screenshots.IsChecked==true,double.IsFinite(age.Value)?(int)age.Value:90);
            var actions=CleanupRulePlanner.Plan(files,rule,DateTime.UtcNow);preview.Text=$"{actions.Count} eligible indexed files\n"+string.Join("\n",actions.Take(15).Select(a=>$"{a.Kind}: {a.File.Name}"));
        };panel.Children.Add(check);panel.Children.Add(preview);
        var dialog=new ContentDialog { XamlRoot=Shell.XamlRoot,RequestedTheme=Shell.RequestedTheme,Title="Automatic cleanup rules",Content=new ScrollViewer { Content=panel,MaxHeight=450 },PrimaryButtonText="Save rules",CloseButtonText="Cancel",DefaultButton=ContentDialogButton.Close };
        dialog.PrimaryButtonClick+=(_,e)=>
        {
            if(recycle.IsChecked!=true&&screenshots.IsChecked!=true)return;
            if(folder.SelectedItem is not IndexedRoot root || string.Equals(Path.TrimEndingDirectorySeparator(root.Path),Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root.Path)!),StringComparison.OrdinalIgnoreCase))
            {e.Cancel=true;preview.Text="Choose a connected folder such as Downloads. Whole-drive rules are not allowed.";}
        };
        if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
        preferences.RuleFolder=(folder.SelectedItem as IndexedRoot)?.Path??"";preferences.RuleRecycleInstallers=recycle.IsChecked==true;preferences.RuleOrganizeScreenshots=screenshots.IsChecked==true;
        preferences.RuleInstallerAgeDays=double.IsFinite(age.Value)?Math.Clamp((int)age.Value,30,3650):90;SavePreferences();nextRuleRun=DateTime.UtcNow.AddMinutes(1);
        Status.Text="Automatic rules saved · Eligible actions start after one minute while the app is running.";
    }
    private async void AutomaticRulesTick(object? sender,object args)
    {
        if(closed||busy||previewOpen||choosingDuplicateFolders||currentCategory=="Duplicates"||selectedPaths.Count>0||DateTime.UtcNow<nextRuleRun||(!preferences.RuleRecycleInstallers&&!preferences.RuleOrganizeScreenshots))return;
        nextRuleRun=DateTime.UtcNow.AddMinutes(30);
        var rule=CurrentRules();if(!roots.Any(r=>string.Equals(r.Path,rule.Folder,StringComparison.OrdinalIgnoreCase)))return;
        var actions=CleanupRulePlanner.Plan(files,rule,DateTime.UtcNow).Take(10).ToArray();if(actions.Length==0)return;
        busy=true;using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(appsLifetime.Token);scanCancellation=cancellation;CancelButton.Content="Cancel rules";CancelButton.Visibility=Visibility.Visible;
        var completed=0;
        try
        {
            foreach(var action in actions)
            {
                cancellation.Token.ThrowIfCancellationRequested();if(CurrentRules()!=rule)break;
                var journal=preferencesPath+".rules.jsonl";
                File.AppendAllText(journal,JsonSerializer.Serialize(new { utc=DateTime.UtcNow,action.Kind,Source=action.File.Path,action.Destination,State="Planned" })+"\n");
                try
                {
                    if(action.Kind=="Recycle installer")await FileCleanup.RecycleAsync(await FileCleanup.PrepareAsync(action.File,cancellation.Token),RecycleBin.Move,cancellation.Token);
                    else await CleanupRulePlanner.MoveScreenshotAsync(action,rule.Folder,cancellation.Token);
                    File.AppendAllText(journal,JsonSerializer.Serialize(new { utc=DateTime.UtcNow,action.Kind,Source=action.File.Path,action.Destination,State="Completed" })+"\n");completed++;
                }
                catch(OperationCanceledException){throw;}
                catch(Exception ex){File.AppendAllText(journal,JsonSerializer.Serialize(new { utc=DateTime.UtcNow,Source=action.File.Path,State="Skipped",Error=ex.Message })+"\n");}
            }
        }
        catch(OperationCanceledException){Status.Text="Automatic rules cancelled · Completed actions remain in history.";}
        catch(Exception ex){Status.Text="Automatic rules stopped: "+ex.Message;}
        finally
        {
            try{if(completed>0&&!closed){await ScanRoot(rule.Folder,CancellationToken.None);RecordActivity($"Automatic rules · {completed} files processed");}}catch(Exception ex){if(!closed)Status.Text="Rules finished; refresh needed: "+ex.Message;}
            scanCancellation=null;busy=false;CancelButton.Visibility=Visibility.Collapsed;CancelButton.Content="Cancel scan";
        }
    }
}
