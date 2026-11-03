using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private DateTime nextSignedUpdateCheck=DateTime.UtcNow.AddMinutes(1);
    private (string Path, Wormy.Core.ReleaseUpdate Release, string Publisher)? pendingSignedUpdate;
    private bool installingAtExit;
    private void InitializeSignedUpdates()
    {
        var choice=new CheckBox { Content="Download trusted signed updates · install on exit",IsChecked=preferences.InstallSignedUpdates };
        ToolTipService.SetToolTip(choice,"Opt-in. Requires release website and pinned publisher certificate in Updates. Contacts that website once daily; signature checks may contact certificate authorities; downloads are hash/signature verified before Windows installer handoff. No library data sent.");
        choice.Click+=(_,_)=> { preferences.InstallSignedUpdates=choice.IsChecked==true; if(!preferences.InstallSignedUpdates) pendingSignedUpdate=null; SavePreferences(); };
        SettingsPanel.Children.Add(choice);
        AppWindow.Closing += InstallPendingOnExit;
        automaticTimer.Tick+=async(_,_)=>
        {
            if(!preferences.InstallSignedUpdates||pendingSignedUpdate is not null||busy||previewOpen||closed||searchCancellation is not null||selectedPaths.Count>0||DateTime.UtcNow<nextSignedUpdateCheck||string.IsNullOrWhiteSpace(preferences.UpdateManifestUrl)||string.IsNullOrWhiteSpace(preferences.UpdatePublisherFingerprint)) return;
            nextSignedUpdateCheck=DateTime.UtcNow.AddDays(1); busy=true;
            try
            {
                var release=await VerifiedUpdates.CheckAsync(preferences.UpdateManifestUrl,appsLifetime.Token);
                var installed=(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"0.0.0").Split('+')[0];
                if(!release.IsNewerThan(installed)) return;
                var publisher=preferences.UpdatePublisherFingerprint;
                Status.Text="Downloading signed app update…"; var path=await VerifiedUpdates.DownloadAsync(release,publisher,appsLifetime.Token);
                if(previewOpen||closed||!preferences.InstallSignedUpdates) return;
                pendingSignedUpdate=(path,release,publisher);
                Status.Text="Verified app update ready · Installer will start when you exit Ceeker.";
            }
            catch(Exception ex) { if(!closed) Status.Text="Signed app update stopped: "+ex.Message; }
            finally { busy=false; }
        };
    }
    private async void InstallPendingOnExit(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if(installingAtExit) { args.Cancel=true; return; }
        if(pendingSignedUpdate is not { } pending || !preferences.InstallSignedUpdates || busy || previewOpen) return;
        args.Cancel=true; installingAtExit=true; busy=true;
        try {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
            Status.Text="Rechecking update before exit…";
            await VerifiedUpdates.LaunchAsync(pending.Path,pending.Release,pending.Publisher,timeout.Token,()=>preferences.InstallSignedUpdates&&!closed&&preferences.UpdatePublisherFingerprint==pending.Publisher);
            pendingSignedUpdate=null; installingAtExit=false; busy=false; Close();
        } catch(Exception ex) { if(!closed) Status.Text="Update was not started: "+ex.Message+" · Exit again to close without installation."; pendingSignedUpdate=null; }
        finally { installingAtExit=false; busy=false; }
    }
}
