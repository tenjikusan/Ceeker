using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Wormy.Core;

namespace Wormy.App;

public sealed partial class MainWindow
{
    private async void RecycleSelected(object sender, RoutedEventArgs args)
    {
        if (busy || previewOpen) return;
        var selected = SelectedFiles().DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        if (selected.Length is 0 or > 100) { Status.Text = "Select between 1 and 100 files to review."; return; }
        busy = true; SyncSelection();
        using var cancellation = new CancellationTokenSource(); scanCancellation = cancellation;
        CancelButton.Content = "Cancel cleanup"; CancelButton.Visibility = Visibility.Visible;
        var requests = new List<RecycleRequest>(); var failures = new List<string>(); var recycled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var file in selected)
            {
                cancellation.Token.ThrowIfCancellationRequested(); Status.Text = $"Preparing file review {requests.Count + failures.Count + 1} of {selected.Length}…";
                try { requests.Add(await FileCleanup.PrepareAsync(file, cancellation.Token)); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { failures.Add($"{file.Path}: {ex.Message}"); }
            }
            cancellation.Token.ThrowIfCancellationRequested();
            if (requests.Count == 0) { Status.Text = "No selected files could be validated."; await ShowReviewFailures(failures); return; }
            var text = new TextBlock { Text = "Only the files listed below will be moved to Windows Recycle Bin. Their contents will be checked again. You can restore them there; disk space is not freed immediately.\n\n" + string.Join("\n\n", requests.Select(r => $"{r.File.Path}\n{r.File.DisplaySize}")) + (failures.Count == 0 ? "" : $"\n\n{failures.Count} unavailable files are excluded."), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            previewOpen = true;
            ContentDialogResult result;
            try
            {
                var dialog = new ContentDialog { XamlRoot = Shell.XamlRoot, RequestedTheme = Shell.RequestedTheme, Title = $"Recycle {requests.Count} selected files?", Content = new ScrollViewer { Content = text, MaxHeight = Math.Max(180, Shell.XamlRoot.Size.Height - 250) }, PrimaryButtonText = "Move to Recycle Bin", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
                result = await dialog.ShowAsync();
            }
            finally { previewOpen = false; }
            if (result != ContentDialogResult.Primary) { Status.Text = "Cleanup cancelled · No files moved"; return; }
            foreach (var request in requests)
            {
                if (cancellation.IsCancellationRequested) break;
                Status.Text = $"Rechecking and recycling {recycled.Count + failures.Count + 1} of {selected.Length}…";
                try { await FileCleanup.RecycleAsync(request, RecycleBin.Move, cancellation.Token); recycled.Add(request.File.Path); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { failures.Add($"{request.File.Path}: {ex.Message}"); }
            }
            files = files.Where(f => !recycled.Contains(f.Path)).ToArray(); selectedPaths.ExceptWith(recycled);
            string? warning = null;
            try { await index.RemoveFilesAsync(recycled); }
            catch (Exception ex) { warning = $"Metadata update failed; refresh the library: {ex.Message}"; }
            RetainDuplicateReviewAfterRecycle(recycled); UpdateCategories(); UpdateFiles();
            Status.Text = $"{recycled.Count} files recycled · {failures.Count} unavailable/failed · {selected.Length - recycled.Count - failures.Count} not processed." + (warning is null ? "" : $" {warning}");
            if (failures.Count > 0) await ShowReviewFailures(failures);
        }
        catch (OperationCanceledException) { if (!closed) Status.Text = "Cleanup preparation cancelled · No files moved"; }
        catch (Exception ex) { if (!closed) Status.Text = $"Cleanup failed: {ex.Message}"; }
        finally { scanCancellation = null; busy = false; CancelButton.Content = "Cancel scan"; CancelButton.Visibility = Visibility.Collapsed; SyncSelection(); }
    }
    private async Task ShowReviewFailures(IReadOnlyList<string> failures)
    {
        if (closed || failures.Count == 0) return;
        previewOpen = true;
        try { await new ContentDialog { XamlRoot = Shell.XamlRoot, RequestedTheme = Shell.RequestedTheme, Title = "Files left in place", CloseButtonText = "Close", Content = new ScrollViewer { MaxHeight = 350, Content = new TextBlock { Text = string.Join("\n\n", failures.Take(100)), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true } } }.ShowAsync(); }
        finally { previewOpen = false; }
    }
    private async void ScanSimilarPhotos(object sender, RoutedEventArgs args)
    {
        if (busy || previewOpen) return;
        busy = true; SimilarScanButton.IsEnabled = false; SimilarPairsPanel.Children.Clear();
        using var cancellation = new CancellationTokenSource(); scanCancellation = cancellation;
        CancelButton.Content = "Cancel photo scan"; CancelButton.Visibility = Visibility.Visible;
        var finished = false;
        try
        {
            var snapshot = files.ToArray();
            var progress = new Progress<int>(count => { if (!finished && !closed && !cancellation.IsCancellationRequested) SimilarSummary.Text = $"Reading image {count:N0} · Up to 2,000 images per scan…"; });
            var sampled=0;
            var result = await Task.Run(() => SimilarPhotos.FindAsync(snapshot, (file,token)=>PhotoSamples.ReadAsync(file,token,Interlocked.Increment(ref sampled)<=100), cancellation.Token, progress, geometricCompare: GeometricPhotos.Compare), cancellation.Token);
            if (closed || cancellation.IsCancellationRequested) return;
            finished = true;
            SimilarSummary.Text = $"{result.Pairs.Count} candidate pairs · {result.Compared} images compared · {result.Skipped} skipped · {result.NotProcessed} beyond the 2,000-image limit." + (result.PairLimitReached ? " Showing the first 300 pairs; additional matches were omitted." : "") + "\nVisual similarity is not proof of identical contents. No files are marked for recycling.";
            foreach (var pair in result.Pairs)
            {
                var panel = new StackPanel { Spacing = 10 };
                panel.Children.Add(new TextBlock { Text = pair.Distance<0 ? $"Geometric candidate · {-pair.Distance} consistent feature matches" : $"Similar appearance · {pair.Distance} visual hash bits differ", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                var pictures = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
                foreach (var file in new[] { pair.First, pair.Second })
                {
                    var card = new FileCard(file); var image = new Image { Height = 110, Stretch = Stretch.Uniform };
                    var column = new StackPanel { Width = 230, Spacing = 6 };
                    var icon = new FontIcon { Glyph = "\uEB9F", FontSize = 30 };
                    var frame = new Grid { Height = 110 }; frame.Children.Add(icon); frame.Children.Add(image);
                    image.Loaded += async (_, _) => { await card.LoadPreviewAsync(); image.Source = card.Preview; icon.Visibility = card.FallbackVisibility; };
                    image.Unloaded += (_, _) => card.CancelPreview();
                    column.Children.Add(frame); column.Children.Add(new TextBlock { Text = file.Name, TextWrapping = TextWrapping.Wrap, FontSize = 12, IsTextSelectionEnabled = true });
                    var copyPath = new Button { Content = "Copy path" };
                    copyPath.Click += (_, _) => { try { Status.Text = FileActions.CopyPath(file.Path); } catch (Exception ex) { Status.Text = $"Unable to copy path: {ex.Message}"; } };
                    column.Children.Add(copyPath); pictures.Children.Add(column);
                }
                panel.Children.Add(pictures);
                var preview = new Button { Content = "Review both originals" }; preview.Click += async (_, _) => await ShowPreview(pair.First, new[] { pair.First, pair.Second }); panel.Children.Add(preview);
                SimilarPairsPanel.Children.Add(new Border { Child = panel, Background = AppTheme.Brush("CardBrush"), CornerRadius = new CornerRadius(12), Padding = new Thickness(16) });
            }
        }
        catch (OperationCanceledException) { SimilarPairsPanel.Children.Clear(); SimilarSummary.Text = "Photo scan cancelled · Run again for complete results."; }
        catch (Exception ex) { SimilarSummary.Text = $"Photo comparison failed: {ex.Message}"; }
        finally { finished = true; scanCancellation = null; busy = false; SimilarScanButton.IsEnabled = true; CancelButton.Content = "Cancel scan"; CancelButton.Visibility = Visibility.Collapsed; }
    }
}

