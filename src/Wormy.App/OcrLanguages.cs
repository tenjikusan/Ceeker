using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private void InitializeOcrLanguages()
    {
        var choices = new ComboBox { Header = "Image OCR language", HorizontalAlignment = HorizontalAlignment.Stretch };
        var languages = new[] { "", "auto", "profile" }.Concat(OcrEngine.AvailableRecognizerLanguages.Select(l=>l.LanguageTag)).ToArray();
        choices.Items.Add("English · bundled offline model"); choices.Items.Add("Automatic · English/Hindi/Telugu/Tamil"); choices.Items.Add("Windows language preferences");
        foreach (var language in OcrEngine.AvailableRecognizerLanguages) choices.Items.Add(language.DisplayName);
        choices.SelectedIndex = Math.Max(0,Array.IndexOf(languages,preferences.OcrLanguage));
        ImageOcr.WindowsLanguage = languages[choices.SelectedIndex];
        choices.SelectionChanged += (_, _) => { if (choices.SelectedIndex < 0) return; preferences.OcrLanguage = languages[choices.SelectedIndex]; ImageOcr.WindowsLanguage = preferences.OcrLanguage; SavePreferences(); Status.Text = "OCR language saved · Existing indexed text is retained; use Index contents to replace it."; };
        SettingsPanel.Children.Add(choices);
    }
}
