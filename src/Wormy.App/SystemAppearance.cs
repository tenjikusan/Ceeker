using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;
namespace Wormy.App;
public sealed partial class MainWindow
{
    private readonly UISettings systemAppearance = new();
    private void InitializeSystemTheme()
    {
        systemAppearance.ColorValuesChanged += SystemColorsChanged;
        Closed += (_, _) => systemAppearance.ColorValuesChanged -= SystemColorsChanged;
        ApplyAppearance();
    }
    private void SystemColorsChanged(UISettings sender, object args) => DispatcherQueue.TryEnqueue(() => { if (!closed && preferences.FollowSystemTheme) ApplyAppearance(); });
    private void ApplyAppearance()
    {
        var foreground = systemAppearance.GetColorValue(UIColorType.Foreground);
        var dark = preferences.FollowSystemTheme ? foreground.R + foreground.G + foreground.B > 382 : preferences.DarkTheme;
        Shell.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        AppTheme.Apply(dark); ApplyVisualStyle();
        themeChoice.IsEnabled = !preferences.FollowSystemTheme;
    }
}
