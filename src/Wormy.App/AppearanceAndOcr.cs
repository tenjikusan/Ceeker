using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Wormy.Core;

namespace Wormy.App;

public sealed partial class MainWindow
{
    private readonly CheckBox themeChoice = new() { Content = "Dark theme" };
    private void ThemeToggled(object sender, RoutedEventArgs args)
    {
        if (restoringPreferences) return;
        preferences.DarkTheme = themeChoice.IsChecked == true;
        ApplyAppearance();
        SavePreferences();
    }
    private static Button? HoverButton(DependencyObject root)
    {
        if (root is Button { Tag: "HoverOcrButton" } button) return button;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (HoverButton(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }
    private void OcrHoverEntered(object sender, PointerRoutedEventArgs args)
    {
        if (sender is DependencyObject root && HoverButton(root) is { } button) { button.Opacity = 1; button.IsHitTestVisible = true; }
    }
    private void OcrHoverExited(object sender, PointerRoutedEventArgs args)
    {
        if (!preferences.TouchMode && sender is DependencyObject root && HoverButton(root) is { FocusState: FocusState.Unfocused } button) { button.Opacity = 0; button.IsHitTestVisible = false; }
    }
    private void OcrButtonFocused(object sender, RoutedEventArgs args)
    { if (sender is Button button) { button.Opacity = 1; button.IsHitTestVisible = true; } }
    private async void CopyHoveredImageText(object sender, RoutedEventArgs args)
    {
        if (busy || previewOpen || sender is not Button { DataContext: FileCard card } button) return;
        if (!SupportsOcr(card.Entry)) return;
        busy = true; button.IsEnabled = false; SyncSelection();
        using var cancellation = new CancellationTokenSource(); scanCancellation = cancellation;
        CancelButton.Content = "Cancel copy text"; CancelButton.Visibility = Visibility.Visible;
        Status.Text = $"Reading {card.Name} locally · English OCR…";
        try
        {
            ThumbnailCache.Validate(card.Entry);
            var result = await ImageOcr.ReadAsync(card.Path, cancellation.Token);
            ThumbnailCache.Validate(card.Entry); cancellation.Token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(result.Text)) { Status.Text = "No text detected · Clipboard unchanged"; return; }
            FileActions.CopyText(result.Text);
            Status.Text = $"Text copied from {card.Name} · Check English OCR for mistakes";
        }
        catch (OperationCanceledException) { if (!closed) Status.Text = "Text copy cancelled · Clipboard unchanged"; }
        catch (Exception ex) { if (!closed) Status.Text = $"Unable to copy image text: {ex.Message}"; }
        finally { scanCancellation = null; busy = false; button.IsEnabled = true; CancelButton.Content = "Cancel scan"; CancelButton.Visibility = Visibility.Collapsed; SyncSelection(); }
    }
}







