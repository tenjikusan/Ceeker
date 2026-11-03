using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private readonly ScrollViewer appsScroll = new() { Visibility = Visibility.Collapsed, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly StackPanel appRows = new() { Spacing = 10 };
    private readonly TextBox appQuery = new() { PlaceholderText = "Search app names or publishers" };
    private readonly CheckBox unusedAppsChoice = new() { Content = "Unused-app candidates · last recorded launch over 90 days ago" };
    private readonly ComboBox appSort = new() { HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock appSummary = new() { Text = "Choose Refresh apps to read installed-app metadata.", TextWrapping = TextWrapping.Wrap };
    private readonly Button appRefresh = new() { Content = "Refresh apps" };
    private readonly Button appCancel = new() { Content = "Cancel refresh", Visibility = Visibility.Collapsed };
    private CancellationTokenSource? appReadingCancellation;
    private readonly CancellationTokenSource appsLifetime = new();
    private InstalledAppResult? appSnapshot;
    private bool readingApps;
    private void InitializeApps()
    {
        var layout = new StackPanel { Spacing = 16 };
        layout.Children.Add(new TextBlock { Text = "Installed apps", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        layout.Children.Add(new TextBlock { Text = "Desktop registrations and packaged apps for this Windows user. Sizes are installer reports, not measured disk usage. Windows handles uninstalling; review the selected app there.", TextWrapping = TextWrapping.Wrap, Foreground = AppTheme.Brush("MutedBrush") });
        appSort.Items.Add("Name A–Z"); appSort.Items.Add("Largest reported size"); appSort.SelectedIndex = 0;
        appQuery.TextChanged += (_, _) => RenderApps(); appSort.SelectionChanged += (_, _) => RenderApps();
        appRefresh.Click += async (_, _) => await ReadApps();
        appCancel.Click += (_, _) => appReadingCancellation?.Cancel();
        var manage = new Button { Content = "Open Windows installed apps" }; manage.Click += async (_, _) => await ManageApp(null);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 }; actions.Children.Add(appRefresh); actions.Children.Add(manage); actions.Children.Add(appCancel);
        unusedAppsChoice.Click += (_, _) => RenderApps();
        layout.Children.Add(actions); layout.Children.Add(appQuery); layout.Children.Add(appSort); layout.Children.Add(unusedAppsChoice);
        layout.Children.Add(new TextBlock { Text = "Launch history is incomplete: Windows UserAssist records some Explorer launches. Unknown history is excluded from unused-app suggestions; recent command-line or other launches may be missing. Review in Windows Settings; no app files are recycled.", TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        layout.Children.Add(appSummary); layout.Children.Add(appRows);
        appsScroll.Content = layout;
        Shell.Loaded += (_, _) =>
        {
            if (BrowseScroll.Parent is Grid parent) { Grid.SetRow(appsScroll, 1); parent.Children.Add(appsScroll); }
            if (HomeButton.Parent is StackPanel navigation)
            {
                var button = new Button { Content = "Apps", Tag = "Apps", Style = (Style)Shell.Resources["NavButton"] }; button.Click += Navigate; navigation.Children.Insert(8, button);
            }
        };
        Closed += (_, _) => appsLifetime.Cancel();
    }
    private async Task ReadApps()
    {
        if (readingApps || closed) return;
        readingApps = true; appRefresh.IsEnabled = false; appSummary.Text = "Reading Windows app registrations…";
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(appsLifetime.Token); appReadingCancellation = cancellation; appCancel.Visibility = Visibility.Visible;
        try { var result = await Task.Run(() => InstalledApps.Read(cancellation.Token), cancellation.Token); cancellation.Token.ThrowIfCancellationRequested(); if (closed) return; var executables = await Task.Run(()=>PortableCandidates.Read(files,result.Apps,cancellation.Token),cancellation.Token); appSnapshot = result with { Apps = result.Apps.Concat(executables).ToArray() }; ApplyUsageObservations(); RenderApps(); UpdateSidebarCounts(); }
        catch (OperationCanceledException) { if (!closed) appSummary.Text = "App refresh cancelled · Previous list retained"; }
        catch (Exception ex) { if (!closed) appSummary.Text = $"App list unavailable: {ex.Message}"; }
        finally { appReadingCancellation = null; appCancel.Visibility = Visibility.Collapsed; readingApps = false; appRefresh.IsEnabled = true; }
    }
    private void RenderApps()
    {
        if (appSnapshot is null) return;
        var matches = InstalledApps.Filter(appSnapshot.Apps.Where(a => unusedAppsChoice.IsChecked != true || AppUsage.IsUnused(a, DateTime.UtcNow)), appQuery.Text, appSort.SelectedIndex == 1);
        appRows.Children.Clear();
        appSummary.Text = $"{matches.Count} matches · {appSnapshot.Apps.Count} registrations · {appSnapshot.Unavailable} unavailable entries/sources" + (appSnapshot.Limited ? " · Enumeration limited to 10,000 entries" : "") + (matches.Count > 300 ? " · Showing the first 300; narrow your search" : "") + ". Unknown sizes sort last. Refresh after installs or uninstalls.";
        foreach (var app in matches.Take(300))
        {
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = app.Name, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock { Text = app.LastObservedLaunchUtc is { } last ? $"Last recorded/observed use: {last.ToLocalTime():d}" : "Last launch: unknown", FontSize = 12 });
            panel.Children.Add(new TextBlock { Text = $"{app.Version} · {app.Publisher}\n{app.Scope}\n" + (app.ReportedBytes is { } bytes ? $"Reported size: {(bytes >= 1_000_000_000 ? $"{bytes / 1_000_000_000d:F1} GB" : $"{bytes / 1_000_000d:F1} MB")}" : "Size unavailable"), TextWrapping = TextWrapping.Wrap, Foreground = AppTheme.Brush("MutedBrush") });
            var button = new Button { Content = app.ExecutablePath is null ? "Manage / uninstall in Windows…" : "Show executable in Explorer", HorizontalAlignment = HorizontalAlignment.Left };
            button.Click += async (_, _) => { if (app.ExecutablePath is { } path) { try { Status.Text=FileActions.Reveal(path); } catch(Exception ex) { Status.Text=ex.Message; } } else await ManageApp(app); }; panel.Children.Add(button);
            if(app.InstallLocation is { Length:>0 } location&&Path.IsPathFullyQualified(location))
            {
                var measure=new Button { Content="Measure installed file storage",HorizontalAlignment=HorizontalAlignment.Left }; var result=new TextBlock { TextWrapping=TextWrapping.Wrap,FontSize=11 };
                measure.Click+=async(_,_)=>
                {
                    measure.IsEnabled=false; using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    try { var report=await Task.Run(()=>Wormy.Core.FolderAllocation.MeasureDirectory(location,cancellation.Token),cancellation.Token); result.Text=$"{report.Measured} measured · {Wormy.Core.LibraryInsights.FormatBytes(report.LogicalBytes)} logical · {Wormy.Core.LibraryInsights.FormatBytes(report.AllocatedBytes)} allocated · {report.Unavailable} unavailable · {report.NotProcessed} omitted/limited. Directory overhead and shared dependencies outside this location are excluded."; }
                    catch(Exception ex) { result.Text="Storage measurement unavailable: "+ex.Message; }
                    finally { measure.IsEnabled=true; }
                };
                panel.Children.Add(measure); panel.Children.Add(result);
            }
            appRows.Children.Add(new Border { Child = panel, Padding = new Thickness(16), CornerRadius = new CornerRadius(12), Background = AppTheme.Brush("CardBrush"), BorderBrush = AppTheme.Brush("LineBrush"), BorderThickness = new Thickness(1) });
        }
    }
    private async Task ManageApp(InstalledApp? app)
    {
        try
        {
            var opened = await Launcher.LaunchUriAsync(InstalledApps.SettingsUri(app));
            if (!opened && app?.PackageFamily is not null) opened = await Launcher.LaunchUriAsync(InstalledApps.SettingsUri());
            if (!closed) Status.Text = opened ? app is null ? "Windows app management opened" : $"Windows app management opened · Select {app.Name} there to review uninstall options" : "Windows app management could not open. Open Settings → Apps → Installed apps.";
        }
        catch (Exception ex) { if (!closed) Status.Text = $"Unable to open Windows app management: {ex.Message}"; }
    }
}

