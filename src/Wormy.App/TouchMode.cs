using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Wormy.App;

public sealed partial class MainWindow
{
    private sealed record TargetSize(double Height, double Width);
    private readonly ConditionalWeakTable<FrameworkElement, TargetSize> originalTargetSizes = new();
    private readonly CheckBox touchChoice = new() { Content = "Touch-screen mode" };
    private bool applyingTouchTargets;
    private bool touchTargetsQueued;
    private void InitializeTouchMode()
    {
        touchChoice.IsChecked = preferences.TouchMode;
        touchChoice.Click += (_, _) =>
        {
            preferences.TouchMode = touchChoice.IsChecked == true;
            SavePreferences(); ApplyTouchMode();
            Status.Text = preferences.TouchMode ? "Touch mode on · Tap to select multiple files, then use Preview selection. Image Copy text is always visible." : "Touch mode off · Standard mouse and keyboard controls restored";
        };
        Shell.Loaded += (_, _) =>
        {
            if (HomeButton.Parent is StackPanel navigation) navigation.Children.Add(touchChoice);
            ApplyTouchMode();
        };
        Shell.LayoutUpdated += (_, _) =>
        {
            if (!preferences.TouchMode || touchTargetsQueued || closed) return;
            touchTargetsQueued = true;
            DispatcherQueue.TryEnqueue(() => { touchTargetsQueued = false; if (preferences.TouchMode && !closed) ApplyTouchMode(); });
        };
    }
    private void ApplyTouchMode()
    {
        if (applyingTouchTargets || closed) return;
        applyingTouchTargets = true;
        try
        {
            var mode = preferences.TouchMode ? ListViewSelectionMode.Multiple : ListViewSelectionMode.Extended;
            if (FileGrid.SelectionMode != mode) FileGrid.SelectionMode = mode;
            if (FileList.SelectionMode != mode) FileList.SelectionMode = mode;
            ApplyTouchTree(Shell);
            if (Shell.XamlRoot is { } root)
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root)) if (popup.Child is { } child) ApplyTouchTree(child);
        }
        finally { applyingTouchTargets = false; }
    }
    private void ApplyTouchTree(DependencyObject root)
    {
        if (root is TextBlock hint && (hint.Text.StartsWith("Click to select ·") || hint.Text.StartsWith("Tap to toggle selection ·")))
        {
            var text = preferences.TouchMode ? "Tap to toggle selection · Use Preview selection to open files · Scroll with a swipe" : "Click to select · Ctrl/Shift for multiple files · Double-click to preview · Drag to copy";
            if (hint.Text != text) hint.Text = text;
        }
        if (root is FrameworkElement element && root is ButtonBase or ComboBox or TextBox or Slider or ListViewItem or GridViewItem)
        {
            if (preferences.TouchMode)
            {
                var size = originalTargetSizes.GetValue(element, e => new(e.MinHeight, e.MinWidth));
                var height = Math.Max(44, size.Height); var width = Math.Max(44, size.Width);
                if (element.MinHeight != height) element.MinHeight = height;
                if (element.MinWidth != width) element.MinWidth = width;
            }
            else if (originalTargetSizes.TryGetValue(element, out var size))
            {
                element.MinHeight = size.Height; element.MinWidth = size.Width; originalTargetSizes.Remove(element);
            }
            if (element is Button { Tag: "HoverOcrButton" } button && preferences.TouchMode) { if (button.Opacity != 1) button.Opacity = 1; if (!button.IsHitTestVisible) button.IsHitTestVisible = true; }
            else if (element is Button { Tag: "HoverOcrButton", FocusState: FocusState.Unfocused } normal) { normal.Opacity = 0; normal.IsHitTestVisible = false; }
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) ApplyTouchTree(VisualTreeHelper.GetChild(root, i));
    }
}
