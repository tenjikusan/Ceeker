using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private Dictionary<string,DateTime> usageObservations=new(StringComparer.OrdinalIgnoreCase);
    private DateTime nextUsageObservation;
    private bool observingUsage;
    private void InitializeUsageObservations()
    {
        try { var path=preferencesPath+".usage.json"; if(File.Exists(path)&&new FileInfo(path).Length<=2_000_000) usageObservations=new Dictionary<string,DateTime>(JsonSerializer.Deserialize<Dictionary<string,DateTime>>(File.ReadAllText(path))??[],StringComparer.OrdinalIgnoreCase); } catch { }
        var choice=new CheckBox { Content="Observe running app use locally",IsChecked=preferences.ObserveAppUsage };
        ToolTipService.SetToolTip(choice,"Optional: while Ceeker is open, record executable path and last observed running time once per minute. No window titles, document names or network telemetry.");
        choice.Click+=(_,_)=> { preferences.ObserveAppUsage=choice.IsChecked==true; SavePreferences(); };
        SettingsPanel.Children.Add(choice);
        var clear=new Button { Content="Clear Ceeker app-use history" };
        clear.Click+=async(_,_)=>
        {
            if(previewOpen||busy) return; SettingsFlyout.Hide(); previewOpen=true;
            try
            {
                var dialog=new ContentDialog { XamlRoot=Shell.XamlRoot,RequestedTheme=Shell.RequestedTheme,Title="Clear local observations?",Content="Discard executable paths and times recorded by Ceeker. This removes the local observation history used by unused-app suggestions.",PrimaryButtonText="Clear history",CloseButtonText="Cancel",DefaultButton=ContentDialogButton.Close };
                if(await dialog.ShowAsync()!=ContentDialogResult.Primary) return;
                usageObservations.Clear(); var path=preferencesPath+".usage.json"; if(File.Exists(path)) File.Delete(path);
                await ReadApps(); Status.Text="Ceeker app-use history cleared";
            }
            catch(Exception ex) { Status.Text="Unable to clear app-use history: "+ex.Message; }
            finally { previewOpen=false; }
        };
        SettingsPanel.Children.Add(clear);
        automaticTimer.Tick+=async(_,_)=>
        {
            if(!preferences.ObserveAppUsage||observingUsage||closed||DateTime.UtcNow<nextUsageObservation) return;
            observingUsage=true; nextUsageObservation=DateTime.UtcNow.AddMinutes(1);
            try
            {
                var observed=await Task.Run(()=>
                {
                    var paths=new List<string>();
                    foreach(var process in Process.GetProcesses().Take(1000))
                    { using(process) { try { if(process.MainModule?.FileName is { } path) if(path.Length<=1024) paths.Add(path); } catch { } } }
                    return paths;
                });
                if(closed||!preferences.ObserveAppUsage) return;
                foreach(var path in observed) usageObservations[path]=DateTime.UtcNow;
                usageObservations=usageObservations.OrderByDescending(p=>p.Value).Take(1000).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.OrdinalIgnoreCase);
                var temporary=preferencesPath+".usage.json.tmp"; File.WriteAllText(temporary,JsonSerializer.Serialize(usageObservations)); File.Move(temporary,preferencesPath+".usage.json",true);
                ApplyUsageObservations(); RenderApps();
            }
            catch(Exception ex) { Status.Text="App-use observations unavailable: "+ex.Message; }
            finally { observingUsage=false; }
        };
    }
    private void ApplyUsageObservations()
    {
        if(appSnapshot is null) return;
        appSnapshot=appSnapshot with { Apps=appSnapshot.Apps.Select(app=>
        {
            var times=usageObservations.Where(p=>app.ExecutablePath is { } path&&string.Equals(path,p.Key,StringComparison.OrdinalIgnoreCase)||app.InstallLocation is { Length:>0 } folder&&p.Key.StartsWith(Path.TrimEndingDirectorySeparator(folder)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)).Select(p=>p.Value).ToArray();
            var time=times.Length==0 ? app.LastObservedLaunchUtc : times.Max();
            if(app.LastObservedLaunchUtc is { } old&&old>time) time=old;
            return app with { LastObservedLaunchUtc=time };
        }).ToArray() };
    }
}
