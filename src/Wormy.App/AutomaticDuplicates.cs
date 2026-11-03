using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private readonly CheckBox automaticDuplicatesChoice = new() { Content = "Automatic duplicate scanning" };
    private bool automaticDuplicatesPending;
    private CancellationTokenSource? automaticDuplicateCancellation;

    private void InitializeAutomaticDuplicates()
    {
        automaticDuplicatesChoice.IsChecked = preferences.AutomaticDuplicates;
        ToolTipService.SetToolTip(automaticDuplicatesChoice, "Compare connected files after library updates while Ceeker is open. Results appear in Duplicates for review.");
        automaticDuplicatesChoice.Click += (_, _) =>
        {
            preferences.AutomaticDuplicates = automaticDuplicatesChoice.IsChecked == true;
            automaticDuplicatesPending = preferences.AutomaticDuplicates && files.Count > 0;
            if (!preferences.AutomaticDuplicates) automaticDuplicateCancellation?.Cancel();
            SavePreferences();
            Status.Text = preferences.AutomaticDuplicates ? "Automatic duplicate comparison enabled · Results will appear in Duplicates" : "Automatic duplicate scanning paused";
        };
        Shell.Loaded += (_, _) => { if (HomeButton.Parent is StackPanel navigation) navigation.Children.Add(automaticDuplicatesChoice); };
        automaticTimer.Tick += AutomaticDuplicatesTick;
    }

    private async void AutomaticDuplicatesTick(object? sender, object args)
    {
        if (closed || busy || previewOpen || choosingDuplicateFolders || !duplicateReview.AllowsAutomaticScan || !preferences.AutomaticDuplicates || !automaticDuplicatesPending) return;
        await RunDuplicateScanAsync(true);
    }
}
