using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wormy.Core;

namespace Wormy.App;

public sealed partial class MainWindow
{
    private async void EditSelectedTags(object sender, RoutedEventArgs args)
    {
        var selected = SelectedFiles();
        if (selected.Length != 1) { Status.Text = "Select one file to edit its tags"; return; }
        await EditTagsAsync(selected[0]);
    }
    private async Task EditTagsAsync(FileEntry file)
    {
        try
        {
            var saved = await index.LoadTagsAsync();
            var input = new TextBox { Header = "Tags separated by commas", Text = saved.TryGetValue(file.Path, out var tags) ? string.Join(", ", tags) : "", MaxLength = 1320, AcceptsReturn = false };
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = file.Name, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(input);
            panel.Children.Add(new TextBlock { Text = "Up to 20 tags, 64 characters each. Clear the field to remove tags. Tags stay in this Ceeker library; your file is unchanged.", TextWrapping = TextWrapping.Wrap });
            var dialog = new ContentDialog { XamlRoot = Shell.XamlRoot, Title = "Edit file tags", Content = panel, PrimaryButtonText = "Save", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close, RequestedTheme = Shell.RequestedTheme };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            await index.SetTagsAsync(file.Path, input.Text.Split(','));
            Status.Text = $"Tags saved for {file.Name}"; UpdateFiles();
        }
        catch (Exception ex) { Status.Text = $"Unable to edit tags: {ex.Message}"; }
    }
}
