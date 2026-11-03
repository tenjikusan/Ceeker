using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private void InitializeInstallerEvidence()
    {
        var button=new Button { Content="Check installer installation status" };
        SettingsPanel.Children.Add(button);
        button.Click+=async(_,_)=> {
            SettingsFlyout.Hide(); if(busy||previewOpen) return;
            var candidates=files.Where(f=>Wormy.Core.CleanupSuggestions.IsOldInstaller(f,DateTime.UtcNow)).Take(100).ToArray();
            busy=true; button.IsEnabled=false;
            try {
                var results=await Task.Run(()=>candidates.Select(f=>{ appsLifetime.Token.ThrowIfCancellationRequested(); try { return f.Name+"\n"+InstallerEvidence.Inspect(f.Path); } catch(Exception ex) { return f.Name+"\nUnavailable · "+ex.Message; } }).ToArray(),appsLifetime.Token);
                if(closed) return; previewOpen=true;
                await new ContentDialog { XamlRoot=Shell.XamlRoot, RequestedTheme=Shell.RequestedTheme, Title="Installer installation evidence", CloseButtonText="Close", Content=new ScrollViewer { MaxHeight=450, Content=new TextBlock { Text="Read-only checks for up to 100 old installer candidates. A registration match does not prove the installer is safe to remove or unnecessary for repair.\n\n"+(results.Length==0 ? "No old installer candidates." : string.Join("\n\n",results)),TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true } } }.ShowAsync();
            } catch(OperationCanceledException) { } catch(Exception ex) { if(!closed) Status.Text="Installer check failed: "+ex.Message; }
            finally { busy=false; previewOpen=false; button.IsEnabled=true; }
        };
    }
}

