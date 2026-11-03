using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Wormy.App;

public sealed partial class MainWindow
{
    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(in Guid id, uint flags, nint token, out nint path);

    private static string? KnownFolder(string id)
    {
        nint path = 0;
        try
        {
            var guid = Guid.Parse(id);
            return SHGetKnownFolderPath(in guid, 0x4000, 0, out path) == 0 ? Marshal.PtrToStringUni(path) : null;
        }
        finally { if (path != 0) Marshal.FreeCoTaskMem(path); }
    }

    private async Task ShowSuggestedFolders()
    {
        if (busy || previewOpen || closed) return;
        previewOpen = true;
        var choices = new List<(string Path, CheckBox Choice)>();
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "Select folders to connect. Only selected folders are scanned. Use + Connect folder for another location.", TextWrapping = TextWrapping.Wrap });
        foreach (var (name, id) in new[]
        {
            ("Desktop", "B4BFCC3A-DB2C-424C-B029-7FE99A87C641"),
            ("Documents", "FDD39AD0-238F-46AF-ADB4-6C85480369C7"),
            ("Downloads", "374DE290-123F-4565-9164-39C4925E467B"),
            ("Pictures", "33E28130-4E1E-4676-835A-98395C3BC3BB")
        })
        {
            var path = KnownFolder(id);
            if (path is null || !Directory.Exists(path) || choices.Any(c => c.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
            var connected = roots.Any(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            var label = new StackPanel();
            label.Children.Add(new TextBlock { Text = name + (connected ? " · Already connected" : "") });
            label.Children.Add(new TextBlock { Text = path, TextWrapping = TextWrapping.Wrap, MaxWidth = 420, Foreground = AppTheme.Brush("MutedBrush") });
            var choice = new CheckBox { Content = label, IsEnabled = !connected };
            choices.Add((path, choice)); panel.Children.Add(choice);
        }
        if (choices.Count == 0) panel.Children.Add(new TextBlock { Text = "No standard folders are available. Use + Connect folder." });
        var dialog = new ContentDialog
        {
            XamlRoot = Shell.XamlRoot, RequestedTheme = Shell.RequestedTheme, Title = "Connect suggested folders",
            Content = new ScrollViewer { Content = panel, MaxHeight = 360, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
            PrimaryButtonText = "Connect selected", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false
        };
        foreach (var item in choices)
        {
            item.Choice.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = choices.Any(c => c.Choice.IsEnabled && c.Choice.IsChecked == true);
            item.Choice.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = choices.Any(c => c.Choice.IsEnabled && c.Choice.IsChecked == true);
        }
        string[] selected = [];
        try { if (await dialog.ShowAsync() == ContentDialogResult.Primary) selected = choices.Where(c => c.Choice.IsEnabled && c.Choice.IsChecked == true).Select(c => c.Path).ToArray(); }
        catch (Exception ex) { if (!closed) Status.Text = $"Folder setup unavailable: {ex.Message}"; }
        finally { previewOpen = false; }
        if (selected.Length == 0 || closed) return;
        busy = true;
        using var cancellation = new CancellationTokenSource();
        scanCancellation = cancellation; CancelButton.Visibility = Visibility.Visible;
        var completed = 0; var failures = new List<string>();
        try
        {
            foreach (var path in selected)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                try { await ScanRoot(path, cancellation.Token); completed++; }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { failures.Add($"{path}: {ex.Message}"); }
            }
            if (!closed) Status.Text = $"Connected {completed} of {selected.Length} selected folders" + (failures.Count > 0 ? $" · {failures.Count} failed · {failures[0]}" : $" · {files.Count:N0} files in your library");
        }
        catch (OperationCanceledException) { if (!closed) Status.Text = $"Folder setup cancelled · {completed} completed folders saved; remaining folders unchanged"; }
        finally { scanCancellation = null; CancelButton.Visibility = Visibility.Collapsed; busy = false; }
    }
}
