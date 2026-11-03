using System.Net.Http;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using Wormy.Core;

namespace Wormy.App;

public sealed partial class MainWindow
{
    private void InitializeUpdates()
    {
        Shell.Loaded += (_, _) =>
        {
            if (HomeButton.Parent is not StackPanel navigation) return;
            var button = new Button { Content = "Updates…", Style = (Style)Shell.Resources["NavButton"] };
            button.Click += async (_, _) => await ShowUpdates(); navigation.Children.Add(button);
        };
    }
    private async Task ShowUpdates()
    {
        if (busy || previewOpen || closed) return;
        previewOpen = true;
        using var lifetime = new CancellationTokenSource();
        var installed = (Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.26.0-preview.1").Split('+')[0];
        var address = new TextBox { Header = "Release manifest HTTPS URL", Text = preferences.UpdateManifestUrl, PlaceholderText = "https://your-website/wormy/latest.json" };
        var status = new TextBlock { Text = "Choose Check now to contact your release website. No files or library data are sent.", TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        var open = new Button { Content = "Open installer download", IsEnabled = false };
        var publisher=new TextBox { Header="Trusted publisher certificate fingerprint",Text=preferences.UpdatePublisherFingerprint,PlaceholderText="Configure when Ceeker has a signing certificate" };
        var install=new Button { Content="Download, verify and install",IsEnabled=false };
        ReleaseUpdate? release = null;
        address.TextChanged += (_, _) => { release = null; open.IsEnabled = false; install.IsEnabled=false; };
        publisher.TextChanged+=(_,_)=> { preferences.UpdatePublisherFingerprint=publisher.Text.Trim(); SavePreferences(); install.IsEnabled=release is not null&&release.IsNewerThan(installed)&&!string.IsNullOrWhiteSpace(publisher.Text); };
        install.Click+=async(_,_)=>
        {
            if(release is null) return; var selectedRelease=release; var selectedPublisher=publisher.Text; install.IsEnabled=false; address.IsEnabled=false; publisher.IsEnabled=false;
            try { status.Text="Downloading and verifying installer…"; var path=await VerifiedUpdates.DownloadAsync(selectedRelease,selectedPublisher,lifetime.Token); await VerifiedUpdates.LaunchAsync(path,selectedRelease,selectedPublisher,lifetime.Token); status.Text="Verified installer started · Follow its installation window."; }
            catch(Exception ex) { status.Text="Update installation stopped: "+ex.Message; }
            finally { address.IsEnabled=true; publisher.IsEnabled=true; install.IsEnabled=release is not null; }
        };
        open.Click += async (_, _) => { if (release is not null) { try { await Launcher.LaunchUriAsync(ReleaseUpdate.Https(release.InstallerUrl)); } catch (Exception ex) { status.Text = ex.Message; } } };
        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(new TextBlock { Text = $"Installed: {installed}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(address); panel.Children.Add(status); panel.Children.Add(open);
        panel.Children.Add(publisher); panel.Children.Add(install);
        var dialog = new ContentDialog { XamlRoot = Shell.XamlRoot, RequestedTheme = Shell.RequestedTheme, Title = "Ceeker updates", Content = new ScrollViewer { Content = panel, MaxHeight = 350 }, PrimaryButtonText = "Check now", CloseButtonText = "Close" };
        dialog.Closing += (_, _) => lifetime.Cancel();
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true; dialog.IsPrimaryButtonEnabled = false; address.IsEnabled = false; open.IsEnabled = false; release = null;
            try
            {
                var uri = ReleaseUpdate.Https(address.Text.Trim());
                preferences.UpdateManifestUrl = uri.AbsoluteUri; SavePreferences(); status.Text = "Checking release website…";
                using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                request.CancelAfter(TimeSpan.FromSeconds(15));
                using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, request.Token);
                response.EnsureSuccessStatusCode();
                using var stream = await response.Content.ReadAsStreamAsync(request.Token);
                using var buffer = new MemoryStream(); var bytes = new byte[4096]; int count;
                while ((count = await stream.ReadAsync(bytes, request.Token)) > 0) { if (buffer.Length + count > 65536) throw new InvalidDataException("Release manifest is too large."); buffer.Write(bytes, 0, count); }
                release = ReleaseUpdate.Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
                if (lifetime.IsCancellationRequested || closed) return;
                var newer = release.IsNewerThan(installed); open.IsEnabled = newer; install.IsEnabled=newer&&!string.IsNullOrWhiteSpace(publisher.Text);
                status.Text = newer ? $"Available: {release.Version}\n{release.Notes}\n\nInstaller SHA256: {release.Sha256}\nUse the verified installation action after configuring the publisher certificate. Automatic updates are optional in Settings." : "You are up to date with this release feed.";
            }
            catch (Exception ex) { if (!lifetime.IsCancellationRequested && !closed) status.Text = $"Update check failed: {ex.Message}"; }
            finally { if (!lifetime.IsCancellationRequested && !closed) { dialog.IsPrimaryButtonEnabled = true; address.IsEnabled = true; } }
        };
        try { await dialog.ShowAsync(); }
        finally { lifetime.Cancel(); previewOpen = false; }
    }
}
