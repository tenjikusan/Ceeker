using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Wormy.Core;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private static string ReadLicenseFile(string path, int limit)
    {
        using var reader=new StreamReader(path); var buffer=new char[limit+1]; var count=reader.ReadBlock(buffer,0,buffer.Length);
        if(count>limit) throw new InvalidDataException("License configuration exceeds size limit.");
        return new string(buffer,0,count);
    }
    private void InitializeLicenseSettings()
    {
        var button=new Button { Content="License and device activation" };
        button.Click+=async(_,_)=> {
            SettingsFlyout.Hide(); if(previewOpen||busy) return; previewOpen=true;
            try {
                var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Wormy","licensing"); Directory.CreateDirectory(folder);
                var deviceFile=Path.Combine(folder,"device.txt");
                if(!File.Exists(deviceFile)) { try { using var writer=new StreamWriter(new FileStream(deviceFile,FileMode.CreateNew,FileAccess.Write,FileShare.None)); writer.Write(Guid.NewGuid().ToString("N")); } catch(IOException) { if(!File.Exists(deviceFile)) throw; } }
                var device=File.ReadAllText(deviceFile).Trim(); if(!Guid.TryParseExact(device,"N",out _)) throw new InvalidDataException("Device identity is invalid. Preserve this file and contact your license issuer.");
                var licenseFile=Path.Combine(folder,"activation.txt"); var keyFile=Path.Combine(AppContext.BaseDirectory,"licenses","issuer-public.pem");
                var panel=new StackPanel { Spacing=12 }; var status=new TextBlock { TextWrapping=TextWrapping.Wrap };
                var input=new TextBox { Header="Signed license", AcceptsReturn=true, MaxLength=16384, Height=100 };
                void Refresh() {
                    if(!File.Exists(keyFile)) { status.Text="Checkout and paid-license enforcement are inactive. No issuer public key is configured."; return; }
                    if(!File.Exists(licenseFile)) { status.Text="No license activated on this device."; return; }
                    try { var claims=OfflineLicense.Verify(ReadLicenseFile(licenseFile,16384),ReadLicenseFile(keyFile,8192),device,DateTimeOffset.UtcNow); status.Text="Activated · "+claims.LicenseId+(claims.ExpiresUtc is null ? " · perpetual" : " · expires "+claims.ExpiresUtc.Value.ToString("u")); }
                    catch(Exception ex) { status.Text="Activation invalid: "+ex.Message; }
                }
                Refresh(); panel.Children.Add(status);
                panel.Children.Add(new TextBlock { Text="Device ID: "+device, IsTextSelectionEnabled=true, TextWrapping=TextWrapping.Wrap });
                var copy=new Button { Content="Copy device ID" }; copy.Click+=(_,_)=>{ var data=new DataPackage(); data.SetText(device); Clipboard.SetContent(data); status.Text="Device ID copied · Give this ID to your license issuer."; }; panel.Children.Add(copy);
                panel.Children.Add(input); var actions=new StackPanel { Orientation=Orientation.Horizontal,Spacing=12 };
                var activate=new Button { Content="Activate",IsEnabled=File.Exists(keyFile) }; activate.Click+=(_,_)=> { try { _=OfflineLicense.Verify(input.Text,ReadLicenseFile(keyFile,8192),device,DateTimeOffset.UtcNow); var temporary=licenseFile+".tmp"; File.WriteAllText(temporary,input.Text.Trim()); File.Move(temporary,licenseFile,true); input.Text=""; Refresh(); } catch(Exception ex) { status.Text="Activation failed: "+ex.Message; } }; actions.Children.Add(activate);
                var deactivate=new Button { Content="Deactivate this device" }; bool armed=false;
                deactivate.Click+=(_,_)=> { if(!armed) { armed=true; deactivate.Content="Confirm local deactivation"; status.Text="This removes local activation. Offline licenses cannot be remotely revoked; transfer requires your issuer to issue a license for the new device."; return; } try { if(File.Exists(licenseFile)) File.Delete(licenseFile); armed=false; deactivate.Content="Deactivate this device"; Refresh(); } catch(Exception ex) { status.Text="Deactivation failed: "+ex.Message; } }; actions.Children.Add(deactivate); panel.Children.Add(actions);
                panel.Children.Add(new TextBlock { Text="Activation is offline. Checkout is inactive. Keep the device ID when reinstalling. Device transfers require a new signed license from the issuer; local deactivation does not prevent restoring an old offline license.",TextWrapping=TextWrapping.Wrap });
                await new ContentDialog { XamlRoot=Shell.XamlRoot,RequestedTheme=Shell.RequestedTheme,Title="Ceeker license",CloseButtonText="Close",Content=panel }.ShowAsync();
            } catch(Exception ex) { if(!closed) Status.Text="License settings failed: "+ex.Message; } finally { previewOpen=false; }
        };
        SettingsPanel.Children.Add(button);
    }
}
