using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wormy.Core;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private readonly CheckBox backgroundOcrChoice = new() { Content = "Background content indexing and OCR" };
    private CancellationTokenSource? backgroundOcrCancellation;
    private DateTime nextOcrCheck;
    private readonly Dictionary<string, (long Length, DateTime Modified, DateTime Retry)> ocrFailures = new(StringComparer.OrdinalIgnoreCase);
    private void InitializeBackgroundOcr()
    {
        backgroundOcrChoice.IsChecked = preferences.BackgroundOcr;
        backgroundOcrChoice.Click += (_, _) => { preferences.BackgroundOcr = backgroundOcrChoice.IsChecked == true; SavePreferences(); if (!preferences.BackgroundOcr) backgroundOcrCancellation?.Cancel(); nextOcrCheck = DateTime.MinValue; Status.Text = preferences.BackgroundOcr ? "Background document indexing and image OCR enabled for connected folders" : "Background OCR paused · Saved text remains searchable"; };
        Shell.Loaded += (_, _) => { if (HomeButton.Parent is StackPanel navigation) navigation.Children.Add(backgroundOcrChoice); };
        automaticTimer.Tick += BackgroundOcrTick;
        Closed += (_, _) => backgroundOcrCancellation?.Cancel();
    }
    private async void BackgroundOcrTick(object? sender, object args)
    {
        if (!preferences.BackgroundOcr || closed || busy || previewOpen || DateTime.UtcNow < nextOcrCheck) return;
        nextOcrCheck = DateTime.UtcNow.AddSeconds(5); busy = true;
        using var cancellation = new CancellationTokenSource(); backgroundOcrCancellation = cancellation;
        scanCancellation = cancellation; CancelButton.Content = "Pause OCR"; CancelButton.Visibility = Visibility.Visible;
        try
        {
            var indexed = await index.SearchContentAsync([], cancellation.Token);
            var file = files.Where(f => SupportsOcr(f) || DocumentText.Supports(f.Extension)).FirstOrDefault(f => !indexed.Contains(f.Path) && (!ocrFailures.TryGetValue(f.Path, out var failure) || failure.Length != f.Length || failure.Modified != f.ModifiedUtc || DateTime.UtcNow >= failure.Retry));
            if (file is null) return;
            try
            {
                Status.Text = $"Background content indexing · {file.Name}";
                ValidateContentFile(file);
                var text = SupportsOcr(file) ? (await ImageOcr.ReadAsync(file.Path,cancellation.Token)).Text : file.Extension == ".pdf" ? (await PdfContent.ReadAsync(file.Path,null,cancellation.Token)).Text : (await DocumentText.ReadAsync(file.Path,cancellation.Token)).Text;
                ValidateContentFile(file);
                await index.StoreContentAsync(file, text[..Math.Min(text.Length, TextPreview.MaximumCharacters)], "Background content indexing", cancellation.Token);
                RecordActivity($"File text indexed · {file.Name}");
                ocrFailures.Remove(file.Path); Status.Text = $"File text indexed automatically · {file.Name}"; UpdateFiles();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { if (ocrFailures.Count >= 2000) ocrFailures.Clear(); ocrFailures[file.Path] = (file.Length, file.ModifiedUtc, DateTime.UtcNow.AddMinutes(5)); Status.Text = $"Background OCR skipped {file.Name}: {ex.Message}"; }
        }
        catch (OperationCanceledException) { if (!closed) { preferences.BackgroundOcr = false; backgroundOcrChoice.IsChecked = false; SavePreferences(); Status.Text = "Background OCR paused · Completed text retained"; } }
        catch (Exception ex) { if (!closed) Status.Text = $"Background OCR delayed: {ex.Message}"; }
        finally { backgroundOcrCancellation = null; scanCancellation = null; busy = false; CancelButton.Content = "Cancel scan"; CancelButton.Visibility = Visibility.Collapsed; }
    }
}
