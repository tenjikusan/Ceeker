using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Wormy.App;

public sealed partial class MainWindow
{
    private bool guideOpen;

    private void InitializeGettingStarted()
    {
        Shell.Loaded += (_, _) =>
        {
            if (HomeButton.Parent is not StackPanel navigation) return;
            var guide = new Button { Content = "Getting started…", Style = (Style)Shell.Resources["NavButton"] };
            guide.Click += async (_, _) => await ShowGettingStarted();
            navigation.Children.Add(guide);
            var folders = new Button { Content = "Suggested folders…", Style = (Style)Shell.Resources["NavButton"] };
            folders.Click += async (_, _) => await ShowSuggestedFolders();
            navigation.Children.Add(folders);
        };
    }

    private async Task ShowGettingStarted()
    {
        if (guideOpen || previewOpen || busy || closed) return;
        guideOpen = true;
        previewOpen = true;
        var step = 0;
        var completed = false;
        var headings = new[] { "Choose your folders", "Find and preview", "Review before cleaning up" };
        var descriptions = new[]
        {
            "Use + Connect folder to choose what appears in your library. Ceeker reads those folders locally and keeps a saved index. Automatic folder updates keep connected files current; pause them in the sidebar. Scanning settings control hidden files and exclusions. Disconnecting a folder leaves its files in place.",
            "Browse categories, switch Grid/List, or search with filters such as PDFs over 5 MB. Double-click a file to preview it. Index file contents enables saved text search; turn on Search file contents to use it. Image and scanned-PDF text recognition runs locally in English. Review recognized text before using it.",
            "Duplicates and Similar photos help you compare originals. Similar photos are suggestions, not proof that files are identical. Select files explicitly and review full paths before confirming recycling. Recycle Bin contents still occupy space. Apps opens Windows Settings for management. You can replay this guide from Getting started in the sidebar."
        };
        var progress = new TextBlock { Foreground = AppTheme.Brush("MutedBrush") };
        var heading = new TextBlock { FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        var description = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 460 };
        var panel = new StackPanel { Spacing = 16 }; panel.Children.Add(progress); panel.Children.Add(heading); panel.Children.Add(description);
        var dialog = new ContentDialog
        {
            XamlRoot = Shell.XamlRoot, RequestedTheme = Shell.RequestedTheme, Title = "Welcome to Ceeker",
            Content = new ScrollViewer { Content = panel, MaxHeight = 360, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
            PrimaryButtonText = "Next", CloseButtonText = "Later", DefaultButton = ContentDialogButton.Primary
        };
        void Render()
        {
            progress.Text = $"Step {step + 1} of 3"; heading.Text = headings[step]; description.Text = descriptions[step];
            dialog.PrimaryButtonText = step == 2 ? "Get started" : "Next";
            dialog.SecondaryButtonText = step > 0 ? "Back" : "";
        }
        dialog.PrimaryButtonClick += (_, args) => { if (step < 2) { args.Cancel = true; step++; Render(); } };
        dialog.SecondaryButtonClick += (_, args) => { args.Cancel = true; step = Math.Max(0, step - 1); Render(); };
        Render();
        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !closed)
            {
                preferences.GettingStartedCompleted = true;
                SavePreferences();
                completed = true;
            }
        }
        catch (Exception ex) { if (!closed) Status.Text = $"Getting-started guide unavailable: {ex.Message}"; }
        finally { guideOpen = false; previewOpen = false; }
        if (completed && roots.Count == 0) await ShowSuggestedFolders();
    }
}
