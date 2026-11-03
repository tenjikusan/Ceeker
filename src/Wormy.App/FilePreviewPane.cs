using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Wormy.Core;

namespace Wormy.App;

/// Read-only previews. No embedded scripts or macros are executed.
public sealed class FilePreviewPane : UserControl
{
    private readonly FileEntry entry;
    private readonly CancellationTokenSource lifetime = new();
    private readonly TextBlock message = new() { Text = "Loading preview…", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly Grid surface = new() { Height = 330, Background = AppTheme.Brush("PreviewBrush") };
    private readonly Image image = new() { Stretch = Stretch.Uniform, Margin = new Thickness(8) };
    private readonly TextBlock pageLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button previous = new() { Content = "Previous", IsEnabled = false };
    private readonly Button next = new() { Content = "Next", IsEnabled = false };
    private PdfDocument? pdf;
    private uint pageNumber;
    private bool started;
    private bool rendering;
    private LocalMedia? media;
    private MediaPlayerElement? mediaElement;
    private readonly Canvas ocrOverlay = new() { IsHitTestVisible = false, Margin = new Thickness(8) };
    private readonly TextBox ocrText = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80, MaxHeight = 120, Visibility = Visibility.Collapsed };
    private ImageOcrResult? ocrResult;
    private readonly HashSet<int> selectedWords = [];
    private readonly StackPanel wordChoices = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly TextBox selectedText = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 80, Visibility = Visibility.Collapsed };
    private readonly Button copySelected = new() { Content = "Copy selected words", IsEnabled = false };
    private readonly TextBlock details = new() { Text = "Reading file details…", FontSize = 12, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };

    public FilePreviewPane(FileEntry file)
    {
        entry = file;
        RequestedTheme = ElementTheme.Default;
        var layout = new StackPanel { Spacing = 12, Width = 640, MaxWidth = 640 };
        layout.Children.Add(new TextBlock { Text = file.Path, TextWrapping = TextWrapping.Wrap, FontSize = 12, IsTextSelectionEnabled = true });
        layout.Children.Add(new TextBlock { Text = file.DownloadSource is null ? "Download source: Unknown" : $"Downloaded from: {file.DownloadSource} · {file.SourceHost}", FontSize = 12, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
        layout.Children.Add(new TextBlock { Text = $"{file.DisplaySize} · Modified {file.ModifiedUtc.ToLocalTime():g}", FontSize = 12 });
        layout.Children.Add(new Expander { Header = "File details", HorizontalAlignment = HorizontalAlignment.Stretch, Content = details });
        surface.Children.Add(image); surface.Children.Add(ocrOverlay); layout.Children.Add(surface);
        surface.SizeChanged += (_, _) => DrawOcr();
        ocrOverlay.PointerPressed += (_, args) =>
        {
            if (ocrResult is null || wordChoices.Children.Count != ocrResult.Words.Count) return;
            var point = args.GetCurrentPoint(ocrOverlay).Position;
            var width = surface.ActualWidth - 16; var height = surface.ActualHeight - 16;
            var scale = Math.Min(width / ocrResult.Width, height / ocrResult.Height);
            var left = (width - ocrResult.Width * scale) / 2; var top = (height - ocrResult.Height * scale) / 2;
            for (var i = 0; i < ocrResult.Words.Count; i++)
            {
                var word = ocrResult.Words[i];
                if (point.X < left + word.X * scale || point.X > left + (word.X + word.Width) * scale || point.Y < top + word.Y * scale || point.Y > top + (word.Y + word.Height) * scale) continue;
                if (!selectedWords.Add(i)) selectedWords.Remove(i);
                ((Microsoft.UI.Xaml.Controls.Primitives.ToggleButton)wordChoices.Children[i]).IsChecked = selectedWords.Contains(i);
                UpdateSelectedWords(); DrawOcr(); args.Handled = true; break;
            }
        };
        var pages = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Visibility = file.Extension == ".pdf" ? Visibility.Visible : Visibility.Collapsed };
        previous.Click += async (_, _) => { if (pageNumber > 0 && !rendering) { pageNumber--; await RenderPage(); } };
        next.Click += async (_, _) => { if (pdf is not null && pageNumber + 1 < pdf.PageCount && !rendering) { pageNumber++; await RenderPage(); } };
        pages.Children.Add(previous); pages.Children.Add(pageLabel); pages.Children.Add(next); layout.Children.Add(pages);
        layout.Children.Add(message);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var open = new Button { Content = "Open", CornerRadius = new CornerRadius(8) };
        open.Click += OpenFile;
        var reveal = new Button { Content = "Show in Explorer", CornerRadius = new CornerRadius(8) };
        reveal.Click += RevealFile;
        var copy = new Button { Content = "Copy path", CornerRadius = new CornerRadius(8) };
        copy.Click += (_, _) =>
        {
            try { message.Text = FileActions.CopyPath(entry.Path); }
            catch (Exception ex) { message.Text = $"Unable to copy path: {ex.Message}"; }
        };
        actions.Children.Add(open); actions.Children.Add(reveal); actions.Children.Add(copy); layout.Children.Add(actions);
        if (ImageOcr.Supports(file.Extension))
        {
            var copyText = new Button { Content = "Copy text", CornerRadius = new CornerRadius(8) };
            copyText.Click += async (_, _) =>
            {
                copyText.IsEnabled = false;
                message.Text = "Reading image text locally…";
                try
                {
                    ocrResult = await ImageOcr.ReadAsync(entry.Path, lifetime.Token);
                    if (lifetime.IsCancellationRequested) return;
                    selectedWords.Clear(); wordChoices.Children.Clear(); ocrOverlay.IsHitTestVisible = false; UpdateSelectedWords();
                    surface.Height = 250;
                    ocrText.Text = ocrResult.Text; ocrText.Visibility = Visibility.Visible; DrawOcr();
                    if (string.IsNullOrWhiteSpace(ocrResult.Text)) { message.Text = "No text detected. Clipboard unchanged."; return; }
                    var data = new DataPackage(); data.SetText(ocrResult.Text); Clipboard.SetContent(data);
                    message.Text = "Text copied · Local OCR · Check recognition for mistakes";
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { if (!lifetime.IsCancellationRequested) message.Text = $"Unable to recognize text: {ex.Message}"; }
                finally { copyText.IsEnabled = !lifetime.IsCancellationRequested; }
            };
            actions.Children.Add(copyText);
            var selectText = new Button { Content = "Select text" };
            selectText.Click += async (_, _) =>
            {
                selectText.IsEnabled = false;
                try
                {
                    message.Text = "Reading image words locally…";
                    ocrResult ??= await ImageOcr.ReadAsync(entry.Path, lifetime.Token);
                    if (lifetime.IsCancellationRequested) return;
                    surface.Height = 250; selectedWords.Clear(); wordChoices.Children.Clear();
                    for (var i = 0; i < ocrResult.Words.Count; i++)
                    {
                        var index = i;
                        var choice = new Microsoft.UI.Xaml.Controls.Primitives.ToggleButton { Content = ocrResult.Words[i].Text, MinHeight = 44, MinWidth = 44 };
                        choice.Click += (_, _) => { if (choice.IsChecked == true) selectedWords.Add(index); else selectedWords.Remove(index); UpdateSelectedWords(); DrawOcr(); };
                        wordChoices.Children.Add(choice);
                    }
                    selectedText.Visibility = Visibility.Visible; UpdateSelectedWords(); DrawOcr();
                    ocrOverlay.IsHitTestVisible = true; ocrOverlay.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    message.Text = ocrResult.Words.Count == 0 ? "No words detected · Clipboard unchanged" : "Tap words below to select them · Recognition order is preserved · Check OCR for mistakes";
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { if (!lifetime.IsCancellationRequested) message.Text = $"Unable to read words: {ex.Message}"; }
                finally { selectText.IsEnabled = !lifetime.IsCancellationRequested; }
            };
            var selectionActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            selectionActions.Children.Add(selectText); selectionActions.Children.Add(copySelected);
            copySelected.Click += (_, _) => { try { if (selectedWords.Count > 0) { FileActions.CopyText(selectedText.Text); message.Text = $"{selectedWords.Count} selected words copied · Check OCR for mistakes"; } } catch (Exception ex) { message.Text = $"Unable to copy words: {ex.Message}"; } };
            layout.Children.Add(selectionActions);
            layout.Children.Add(new ScrollViewer { Content = wordChoices, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 70 });
            layout.Children.Add(selectedText);
        }
        layout.Children.Add(ocrText);
        if (file.Extension == ".pdf")
        {
            var read = new Button { Content = "Read PDF text", CornerRadius = new CornerRadius(8) };
            var copyPdf = new Button { Content = "Copy PDF text", IsEnabled = false, CornerRadius = new CornerRadius(8) };
            var cancel = new Button { Content = "Cancel reading", Visibility = Visibility.Collapsed };
            CancellationTokenSource? reading = null;
            cancel.Click += (_, _) => reading?.Cancel();
            copyPdf.Click += (_, _) =>
            {
                try { FileActions.CopyText(ocrText.Text); message.Text = "PDF text copied · Check OCR for recognition mistakes"; }
                catch (Exception ex) { message.Text = $"Unable to copy PDF text: {ex.Message}"; }
            };
            read.Click += async (_, _) =>
            {
                read.IsEnabled = false; copyPdf.IsEnabled = false; ocrText.Text = ""; ocrText.Visibility = Visibility.Collapsed; cancel.Visibility = Visibility.Visible;
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); reading = cancellation;
                var finished = false;
                var progress = new Progress<string>(value => { if (!finished && !cancellation.IsCancellationRequested) message.Text = value; });
                try
                {
                    ThumbnailCache.Validate(entry);
                    var result = await PdfContent.ReadAsync(entry.Path, progress, cancellation.Token);
                    ThumbnailCache.Validate(entry); cancellation.Token.ThrowIfCancellationRequested();
                    finished = true;
                    ocrText.Text = result.Text; ocrText.Visibility = Visibility.Visible; copyPdf.IsEnabled = true;
                    message.Text = $"PDF text ready · Local OCR pages: {result.OcrPages}" + (result.Truncated ? " · Limited output; some content omitted" : "") + " · Review before copying";
                }
                catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) message.Text = "PDF reading cancelled · Clipboard unchanged"; }
                catch (Exception ex) { if (!lifetime.IsCancellationRequested) message.Text = $"Unable to read PDF text: {ex.Message}"; }
                finally { finished = true; reading = null; cancel.Visibility = Visibility.Collapsed; read.IsEnabled = !lifetime.IsCancellationRequested; }
            };
            var pdfActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            pdfActions.Children.Add(read); pdfActions.Children.Add(copyPdf); pdfActions.Children.Add(cancel); layout.Children.Add(pdfActions);
        }
        Content = layout;
        Loaded += LoadPreview;
    }

    public void Close() { lifetime.Cancel(); mediaElement?.SetMediaPlayer(null); media?.Dispose(); media = null; image.Source = null; pdf = null; }
    private void DrawOcr()
    {
        ocrOverlay.Children.Clear();
        if (ocrResult is null || surface.ActualWidth <= 16) return;
        var width = surface.ActualWidth - 16; var height = surface.ActualHeight - 16;
        var scale = Math.Min(width / ocrResult.Width, height / ocrResult.Height);
        var left = (width - ocrResult.Width * scale) / 2; var top = (height - ocrResult.Height * scale) / 2;
        for (var index = 0; index < ocrResult.Words.Count; index++)
        {
            var word = ocrResult.Words[index];
            var box = new Border { Width = word.Width * scale, Height = word.Height * scale, BorderThickness = new Thickness(selectedWords.Contains(index) ? 2 : 1), BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 89, 165)), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(selectedWords.Contains(index) ? (byte)140 : (byte)40, 56, 89, 165)) };
            Canvas.SetLeft(box, left + word.X * scale); Canvas.SetTop(box, top + word.Y * scale); ocrOverlay.Children.Add(box);
        }
    }
    private void UpdateSelectedWords()
    {
        selectedText.Text = ocrResult is null ? "" : string.Join(" ", selectedWords.Order().Select(i => ocrResult.Words[i].Text));
        copySelected.IsEnabled = selectedWords.Count > 0;
    }
    private CancellationTokenSource Operation()
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        cancellation.CancelAfter(TimeSpan.FromSeconds(20));
        return cancellation;
    }

    private async void LoadPreview(object sender, RoutedEventArgs args)
    {
        if (started) return;
        started = true;
        try
        {
            using var cancellation = Operation();
            try
            {
                var actual = await Task.Run(() => FileInspection.Read(entry.Path), cancellation.Token).WaitAsync(cancellation.Token);
                if (lifetime.IsCancellationRequested) return;
                var changed = actual.Length != entry.Length || actual.ModifiedUtc != entry.ModifiedUtc;
                details.Text = $"Kind: {entry.Category} · {(entry.Extension.Length == 0 ? "No extension" : entry.Extension)}\nFolder: {System.IO.Path.GetDirectoryName(entry.Path)}\nSize: {actual.Length:N0} bytes\nAllocated size: {(actual.AllocatedBytes is { } allocated ? allocated.ToString("N0") + " bytes" : "Unavailable")}\nAdded to Ceeker: {(entry.AddedUtc is { } added ? added.ToLocalTime().ToString("g") : "Unknown")}\nCreated: {actual.CreatedUtc.ToLocalTime():g}\nModified: {actual.ModifiedUtc.ToLocalTime():g}\nLast access reported by Windows: {actual.AccessedUtc.ToLocalTime():g}\nAttributes: {actual.Attributes}" + (changed ? "\nFile changed since indexing. Refresh the library for updated search metadata." : "");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { details.Text = $"Details unavailable: {ex.Message}"; }
            var info = new FileInfo(entry.Path);
            if (!info.Exists) throw new FileNotFoundException("File is no longer available. Refresh the library.");
            if (entry.Category is "Music" or "Videos")
            {
                var player = new LocalMedia(); media = player;
                if (entry.Category == "Music") surface.Height = 120;
                var dispatcher = DispatcherQueue;
                player.Player.MediaOpened += (_, _) => dispatcher.TryEnqueue(() => { if (!lifetime.IsCancellationRequested) message.Text = "Ready · Press Play · Playback stops when this preview closes"; });
                player.Player.MediaFailed += (_, args) => dispatcher.TryEnqueue(() => { if (!lifetime.IsCancellationRequested) message.Text = $"Playback unavailable: {args.ErrorMessage}. Use Open or check your Windows codecs."; });
                mediaElement = new MediaPlayerElement { AreTransportControlsEnabled = true, AutoPlay = false, Stretch = Stretch.Uniform };
                var elementLoaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                mediaElement.Loaded += (_, _) => elementLoaded.TrySetResult();
                surface.Children.Clear(); surface.Children.Add(mediaElement);
                message.Text = "Opening local media…";
                await elementLoaded.Task.WaitAsync(cancellation.Token);
                await player.OpenAsync(entry, cancellation.Token);
                if (lifetime.IsCancellationRequested) return;
                mediaElement.SetMediaPlayer(player.Player);
                return;
            }
            if (info.Length > 100_000_000) { message.Text = "Preview is limited to files under 100 MB. Use Open to view this file."; return; }
            var extension = entry.Extension;
            if (extension == ".pdf")
            {
                var storageFile = await StorageFile.GetFileFromPathAsync(entry.Path).AsTask(cancellation.Token);
                pdf = await PdfDocument.LoadFromFileAsync(storageFile).AsTask(cancellation.Token);
                await RenderPage();
            }
            else if (ImageOcr.Supports(extension))
            {
                var storageFile = await StorageFile.GetFileFromPathAsync(entry.Path).AsTask(cancellation.Token);
                using var stream = await storageFile.OpenReadAsync().AsTask(cancellation.Token);
                var bitmap = new BitmapImage { DecodePixelWidth = 1200 };
                await bitmap.SetSourceAsync(stream).AsTask(cancellation.Token);
                if (lifetime.IsCancellationRequested) return;
                image.Source = bitmap;
                message.Text = "Image preview · Read only";
            }
            else if (DocumentText.Supports(extension))
            {
                var result = await DocumentText.ReadAsync(entry.Path, cancellation.Token);
                if (lifetime.IsCancellationRequested) return;
                var text = new TextBlock { Text = result.Text, Foreground = AppTheme.Brush("TextBrush"), IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Cascadia Mono"), FontSize = 12, Margin = new Thickness(16) };
                surface.Children.Clear(); surface.Children.Add(new ScrollViewer { Content = text, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
                message.Text = result.Truncated ? "Preview truncated at 64,000 characters · Original file unchanged" : extension == ".docx" ? "DOCX body text · Formatting and embedded objects omitted" : "Text preview · Read only";
            }
            else { message.Text = "Preview isn't available for this format yet. Use Open to view it in its default application."; }
        }
        catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) message.Text = "Preview timed out. Use Open or try again."; }
        catch (Exception ex) { if (!lifetime.IsCancellationRequested) message.Text = $"Unable to preview this file: {ex.Message}"; }
    }

    private async Task RenderPage()
    {
        if (pdf is null || rendering || lifetime.IsCancellationRequested) return;
        rendering = true; previous.IsEnabled = false; next.IsEnabled = false;
        try
        {
            using var cancellation = Operation();
            using var page = pdf.GetPage(pageNumber);
            using var stream = new InMemoryRandomAccessStream();
            var scale = Math.Min(1200 / Math.Max(page.Size.Width, 1), 1200 / Math.Max(page.Size.Height, 1));
            await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = (uint)Math.Max(1, page.Size.Width * scale), DestinationHeight = (uint)Math.Max(1, page.Size.Height * scale) }).AsTask(cancellation.Token);
            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream).AsTask(cancellation.Token);
            if (lifetime.IsCancellationRequested) return;
            image.Source = bitmap; pageLabel.Text = $"Page {pageNumber + 1} of {pdf.PageCount}";
            message.Text = "PDF preview · Read only";
        }
        catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) message.Text = "Page preview timed out."; }
        catch (Exception ex) { if (!lifetime.IsCancellationRequested) message.Text = $"Unable to render this page: {ex.Message}"; }
        finally
        {
            rendering = false;
            previous.IsEnabled = pdf is not null && pageNumber > 0;
            next.IsEnabled = pdf is not null && pageNumber + 1 < pdf.PageCount;
        }
    }

    private async void OpenFile(object sender, RoutedEventArgs args)
    {
        try
        {
            message.Text = await FileActions.OpenAsync(entry.Path);
        }
        catch (Exception ex) { message.Text = $"Unable to open file: {ex.Message}"; }
    }
    private void RevealFile(object sender, RoutedEventArgs args)
    {
        try
        {
            message.Text = FileActions.Reveal(entry.Path);
        }
        catch (Exception ex) { message.Text = $"Unable to reveal file: {ex.Message}"; }
    }
}

