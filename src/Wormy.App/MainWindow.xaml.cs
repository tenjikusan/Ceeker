using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.Storage.Pickers;
using Wormy.Core;
using Wormy.Infrastructure;

namespace Wormy.App;

public sealed record CategoryCard(string Name, string Glyph, SolidColorBrush Background, string Summary)
{
    public string Artwork => "ms-appx:///Assets/" + (Name switch
    {
        "Documents" => "ivory-folder.png",
        "Images" => "ivory-folder-images.png",
        "Videos" => "ivory-folder-videos.png",
        "Downloads" => "ivory-folder-downloads.png",
        _ => "ivory-folder-other.png"
    });
    public Visibility ArtworkVisibility => AppTheme.Minimal ? Visibility.Collapsed : Visibility.Visible;
    public double SymbolSize => AppTheme.Minimal ? 64 : 20;
    public Thickness SymbolMargin => AppTheme.Minimal ? new Thickness(0) : new Thickness(36,126,0,0);
    public HorizontalAlignment SymbolHorizontal => AppTheme.Minimal ? HorizontalAlignment.Center : HorizontalAlignment.Left;
    public VerticalAlignment SymbolVertical => AppTheme.Minimal ? VerticalAlignment.Center : VerticalAlignment.Top;
    public Visibility SymbolVisibility => AppTheme.Minimal || Name is "Music" or "Archives" or "Apps" ? Visibility.Visible : Visibility.Collapsed;
}

public sealed class FileCard(FileEntry entry, double width = 180) : System.ComponentModel.INotifyPropertyChanged
{
    private static readonly ThumbnailCache thumbnails = new(NativeThumbnails.ReadAsync,maximumEntries:256);
    private CancellationTokenSource? thumbnailCancellation;
    private Task? thumbnailTask;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public double TileWidth { get; private set; } = width;
    public double PreviewHeight => TileWidth * 0.75;
    public void Resize(double value) { TileWidth = value; PropertyChanged?.Invoke(this, new(nameof(TileWidth))); PropertyChanged?.Invoke(this, new(nameof(PreviewHeight))); }
    public FileEntry Entry { get; } = entry;
    public string Name => Entry.Name;
    public string Path => Entry.Path;
    public string FolderSummary => string.Join(" / ", (System.IO.Path.GetDirectoryName(Entry.Path) ?? "").Split([System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar],StringSplitOptions.RemoveEmptyEntries).TakeLast(3));
    public string DisplaySize => Entry.DisplaySize;
    public double SizePercent { get; init; }
    public Visibility SizeBarVisibility { get; init; } = Visibility.Collapsed;
    public string SizeComparison => $"{SizePercent:0.#}% of the largest matching file";
    public string SourceSummary => Entry.DownloadSource is null ? "Source unknown" : $"From {Entry.DownloadSource}";
    public Visibility OcrVisibility => ImageOcr.Supports(Entry.Extension) ? Visibility.Visible : Visibility.Collapsed;
    public string ReviewReason { get; init; } = "";
    public Visibility ReviewReasonVisibility => ReviewReason.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    public string Glyph => Entry.Category switch { "Images" => "\uEB9F", "Videos" => "\uE714", "Music" => "\uE8D6", "Archives" => "\uF012", _ => "\uE8A5" };
    public ImageSource? Preview { get; private set; }
    public Visibility FallbackVisibility => Preview is null ? Visibility.Visible : Visibility.Collapsed;
    public Stretch PreviewStretch { get; private set; } = Stretch.Uniform;
    public string PreviewDescription { get; private set; } = "File icon";
    public async Task LoadPreviewAsync()
    {
        if (thumbnailTask is { } pending) await pending;
        if (Preview is not null) return;
        var task = LoadPreviewCoreAsync(); thumbnailTask = task;
        try { await task; }
        finally { if (thumbnailTask == task) thumbnailTask = null; }
    }
    private async Task LoadPreviewCoreAsync()
    {
        using var cancellation = new CancellationTokenSource(); thumbnailCancellation = cancellation;
        try
        {
            var data = await thumbnails.GetAsync(Entry, cancellation.Token);
            if (data is null || cancellation.IsCancellationRequested) return;
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            using var writer = new Windows.Storage.Streams.DataWriter(stream);
            writer.WriteBytes(data.Bytes); await writer.StoreAsync(); stream.Seek(0);
            var bitmap = new BitmapImage { DecodePixelWidth = 256 }; await bitmap.SetSourceAsync(stream);
            if (cancellation.IsCancellationRequested) return;
            PreviewStretch = Stretch.Uniform;
            PreviewDescription = data.IsIcon ? "File icon" : "File preview"; Preview = bitmap;
            PropertyChanged?.Invoke(this, new(nameof(PreviewStretch))); PropertyChanged?.Invoke(this, new(nameof(PreviewDescription))); PropertyChanged?.Invoke(this, new(nameof(Preview)));
            PropertyChanged?.Invoke(this, new(nameof(FallbackVisibility)));
        }
        catch { /* Keep the category icon for unavailable previews. */ }
        finally { if (thumbnailCancellation == cancellation) thumbnailCancellation = null; }
    }
    public void CancelPreview() => thumbnailCancellation?.Cancel();
}

public sealed partial class MainWindow : Window
{
    private async void ThumbnailLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { DataContext: FileCard card } element)
        {
            if (element is Image)
            {
                DependencyObject? owner = element;
                StackPanel? container = null;
                while (owner is FrameworkElement candidate && ReferenceEquals(candidate.DataContext, card))
                {
                    container = candidate as StackPanel ?? (candidate as Grid)?.Children.OfType<StackPanel>().FirstOrDefault();
                    if (container is not null) break;
                    owner = VisualTreeHelper.GetParent(owner);
                }
                if (container is not null && !container.Children.OfType<Button>().Any(b => Equals(b.Tag, "HoverOcrButton")))
                {
                    var button = new Button { Content = "Copy text", Tag = "HoverOcrButton", Opacity = 0, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, FontSize = 11, Padding = new Thickness(8,4,8,4) };
                    button.Click += CopyHoveredImageText; button.GotFocus += OcrButtonFocused;
                    var pointerInside = false;
                    container.PointerEntered += (_, _) => pointerInside = true;
                    container.PointerExited += (_, _) => pointerInside = false;
                    button.LostFocus += (_, _) => { if (!preferences.TouchMode && !pointerInside) { button.Opacity = 0; button.IsHitTestVisible = false; } };
                    button.SetBinding(UIElement.VisibilityProperty, new Microsoft.UI.Xaml.Data.Binding { Path = new PropertyPath("OcrVisibility") });
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Copy image text with local OCR");
                    ToolTipService.SetToolTip(button, "Recognize and copy English text from this image");
                    container.Children.Add(button); container.PointerEntered += OcrHoverEntered; container.PointerExited += OcrHoverExited;
                }
            }
            await card.LoadPreviewAsync();
        }
    }
    private void ThumbnailUnloaded(object sender, RoutedEventArgs args)
    { if (sender is FrameworkElement { DataContext: FileCard card }) card.CancelPreview(); }
    private IReadOnlyList<FileEntry> files = [];
    private CancellationTokenSource? scanCancellation;
    private string currentCategory = "Home";
    private sealed record BrowseRoute(string Category, string? Folder, string Query, bool Contents, string Source, bool CurrentView = false);
    private readonly NavigationHistory<BrowseRoute> history = new(new("Home", null, "", false, "All sources"));
    private bool restoringRoute;
    private Button? activeNavigation;
    private readonly LibraryIndex index;
    private readonly FolderChanges folderChanges;
    private readonly DispatcherTimer automaticTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime nextReconcile = DateTime.UtcNow.AddMinutes(1);
    private bool closed;
    private IReadOnlyList<IndexedRoot> roots = [];
    private bool busy = true;
    private bool loaded;
    private readonly string preferencesPath;
    private BrowsePreferences preferences = new();
    private bool restoringPreferences = true;
    private string? pinnedFolder;
    private string? preferenceLoadError;
    private bool updatingSourcePicker;
    private bool previewOpen;
    private FilePreviewPane? currentPreviewPane;
    private bool syncingSelection;
    private readonly HashSet<string> selectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? startupPreviewPath;
    private CancellationTokenSource? searchCancellation;
    private readonly List<DuplicateSelection> duplicateSelections = [];
    private sealed class DuplicateSelection(DuplicateGroup group)
    {
        public FileEntry Keep { get; set; } = group.Files[0];
        public Dictionary<FileEntry, CheckBox> Choices { get; } = [];
    }

    private void ClearDuplicates()
    {
        duplicateReview.Begin();
        dashboardDuplicateGroups = null;
        UpdateDashboard();
        if (SimilarPairsPanel is not null) { SimilarPairsPanel.Children.Clear(); SimilarSummary.Text = "Library changed · Run a new similarity scan."; }
        duplicateSelections.Clear();
        DuplicateGroupsPanel.Children.Clear();
        CleanupButton.IsEnabled = false;
    }

    private void UpdateCleanupButton() => CleanupButton.IsEnabled = !busy && duplicateSelections.Any(selection => selection.Choices.Any(pair => pair.Key != selection.Keep && pair.Value.IsChecked == true));

    private readonly bool testLibrarySession;
    public MainWindow(string? previewPath = null, string? libraryPath = null,bool isTestLibrary=false)
    {
        testLibrarySession=isTestLibrary;
        var databasePath = System.IO.Path.GetFullPath(libraryPath ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wormy", "library.db"));
        index = new LibraryIndex(databasePath);
        folderChanges = new FolderChanges(databasePath);
        startupPreviewPath = previewPath;
        preferencesPath = databasePath + ".view.json";
        try { preferences = BrowsePreferences.Load(preferencesPath); }
        catch (Exception ex) { preferenceLoadError = ex.Message; }
        InitializeComponent();
        Shell.SizeChanged+=(_,_)=>RenderFilterChips();
        InitializeApps();
        InitializeFileShortcuts();
        InitializeFolderHierarchy();
        InitializeFeedback();
        Shell.Loaded += (_, _) => InitializeSidebar();
        InitializeGettingStarted();
        InitializeUpdates();
        InitializeTouchMode();
        themeChoice.Click += ThemeToggled; themeChoice.IsChecked = preferences.DarkTheme;
        Shell.Loaded += (_, _) =>
        {
            DependencyObject? owner = RootPicker;
            while (owner is not null && owner is not StackPanel) owner = VisualTreeHelper.GetParent(owner);
            if (owner is StackPanel controls && themeChoice.Parent is null) controls.Children.Add(themeChoice);
        };
        InitializeSystemTheme();
        Shell.Loaded += (_,_)=>InitializeDesktopIntegration();
        
        Shell.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        AddShortcut(VirtualKey.Left, VirtualKeyModifiers.Menu, () => GoBack(this, new RoutedEventArgs()));
        AddShortcut(VirtualKey.Right, VirtualKeyModifiers.Menu, () => GoForward(this, new RoutedEventArgs()));
        AddShortcut(VirtualKey.F, VirtualKeyModifiers.Control, () => { SearchBox.Focus(FocusState.Keyboard); SearchBox.SelectAll(); });
        AddShortcut(VirtualKey.R, VirtualKeyModifiers.Control, () => RefreshLibrary(this, new RoutedEventArgs()));
        AddShortcut(VirtualKey.F5, VirtualKeyModifiers.None, () => RefreshLibrary(this, new RoutedEventArgs()));
        AddShortcut(VirtualKey.O, VirtualKeyModifiers.Control, () => ChooseFolder(this, new RoutedEventArgs()));
        AddShortcut(VirtualKey.H, VirtualKeyModifiers.Control, () => NavigateTo("Home"));
        AddShortcut(VirtualKey.D, VirtualKeyModifiers.Control, () => NavigateTo("Duplicates"));
        var sections = new[] { "All files", "Images", "Videos", "Music", "Documents", "Archives" };
        for (var i = 0; i < sections.Length; i++) { var section = sections[i]; AddShortcut((VirtualKey)((int)VirtualKey.Number1 + i), VirtualKeyModifiers.Control, () => NavigateTo(section)); }
        SortPicker.SelectedIndex = preferences.Sort;
        SearchContents.IsChecked = preferences.IncludeContents;
        ThumbnailSize.Value = preferences.TileWidth;
        SetBrowseView(preferences.ListView);
        restoringPreferences = false;
        Title = testLibrarySession ? "Ceeker · TEST LIBRARY" : "Ceeker";
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory,"Assets","ceeker.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1440, 1000));
        activeNavigation = HomeButton;
        UpdateCategories();
        Shell.Loaded += LoadLibrary;
        automaticTimer.Tick += AutomaticTick;
        InitializeAutomaticDuplicates();
        InitializeBackgroundOcr();
        Shell.Loaded += (_, _) => MoveSettingsToMenu();
        Closed += (_, _) => { closed = true; currentPreviewPane?.Close(); automaticTimer.Stop(); folderChanges.Dispose(); scanCancellation?.Cancel(); searchCancellation?.Cancel(); foreach (var card in FileGrid.Items.OfType<FileCard>()) card.CancelPreview(); };
    }

    private async void LoadLibrary(object sender, RoutedEventArgs args)
    {
        if (loaded) return;
        loaded = true;
        try { await ReloadSnapshot(); Status.Text = preferenceLoadError is not null ? $"View preferences could not be loaded; defaults used: {preferenceLoadError}" : roots.Count == 0 ? "Connect a folder to start your library" : "Saved library restored · Automatic folder updates on"; }
        catch (Exception ex) { Status.Text = $"Unable to load saved library: {ex.Message}"; }
        finally { busy = false; }
        if (!closed) { automaticTimer.Start(); ApplyExternalCommand(); }
        if (!closed && startupPreviewPath is null && preferenceLoadError is null && roots.Count == 0 && !preferences.GettingStartedCompleted)
            await ShowGettingStarted();
        if (startupPreviewPath is not null)
        {
            try
            {
                var info = new FileInfo(startupPreviewPath);
                await ShowPreview(new FileEntry(info.FullName, info.Name, info.Length, info.LastWriteTimeUtc));
            }
            catch (Exception ex) { Status.Text = $"Unable to open startup preview: {ex.Message}"; }
        }
    }

    private async Task ReloadSnapshot()
    {
        var selectedPath = (RootPicker.SelectedItem as IndexedRoot)?.Path;
        var snapshot = await index.LoadAsync();
        if (closed) return;
        var duplicateMetadataChanged = !files.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .Select(file => (file.Path.ToUpperInvariant(), file.Length, file.ModifiedUtc))
            .SequenceEqual(snapshot.Files.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                .Select(file => (file.Path.ToUpperInvariant(), file.Length, file.ModifiedUtc)));
        files = snapshot.Files; roots = snapshot.Roots;
        UpdateDashboardVolumes();
        LocationLinks.Children.Clear();
        foreach (var root in roots)
        {
            var location = new Button { Content = root.Name, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8,5,8,5), Background = AppTheme.Brush("SidebarBrush"), BorderThickness = new Thickness(0) };
            ToolTipService.SetToolTip(location,root.Path);
            location.Click += (_, _) => NavigateTo(root.Name,root.Path, navigation: location);
            var locationRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 }; locationRow.Children.Add(new FontIcon { Glyph = "\uE8B7", FontSize = 18, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255,55,160,211)) }); locationRow.Children.Add(new TextBlock { Text = root.Name, FontFamily = new FontFamily("Segoe UI"), FontSize = 14 }); location.Content = locationRow; location.MinHeight = 40; LocationLinks.Children.Add(location);
        }
        folderChanges.SetRoots(AutomaticUpdates.IsChecked == true ? roots.Select(r => r.Path) : []);
        if (duplicateMetadataChanged)
        {
            var wasStale=duplicateReview.Stale;
            duplicateReview.LibraryChanged();
            if(duplicateReview.Ready && !wasStale)
                DuplicateSummary.Text += " Library changed since this scan · Results retained for review; contents are rechecked before recycling.";
            else if(!duplicateReview.Ready) DuplicateSummary.Text="Files changed · Run a duplicate scan.";
        }
        automaticDuplicatesPending |= duplicateMetadataChanged && preferences.AutomaticDuplicates && files.Count > 0;
        RootPicker.ItemsSource = roots;
        RootPicker.SelectedItem = roots.FirstOrDefault(root => root.Path == selectedPath) ?? roots.FirstOrDefault();
        FolderLabel.Text = roots.Count == 0 ? "No folder connected" : $"{roots.Count} connected folder{(roots.Count == 1 ? "" : "s")}";
        if (pinnedFolder is not null && !roots.Any(r => string.Equals(r.Path, pinnedFolder, StringComparison.OrdinalIgnoreCase))) { pinnedFolder = null; currentCategory = "All files"; }
        updatingSourcePicker = true;
        var selectedSource = SourcePicker.SelectedItem as string;
        SourcePicker.ItemsSource = new[] { "All sources", "Unknown source" }.Concat(files.Select(f => f.DownloadSource).Where(s => s is not null).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase)).ToArray();
        SourcePicker.SelectedItem = SourcePicker.Items.Contains(selectedSource) ? selectedSource : "All sources";
        updatingSourcePicker = false;
        UpdatePinnedFolders();
        UpdateCategories(); UpdateFiles();
        RecordActivity($"Library snapshot loaded · {files.Count:N0} files");
    }

    private void RootSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (RootPicker is not null) ToolTipService.SetToolTip(RootPicker, (RootPicker.SelectedItem as IndexedRoot)?.Path ?? "Connect a folder to start");
    }

    private async Task ScanRoot(string path, CancellationToken cancellationToken)
    {
        Status.Text = $"Scanning {path}…";
        var result = await FolderScanner.ScanAsync(path, cancellationToken, CurrentScanOptions());
        cancellationToken.ThrowIfCancellationRequested();
        if (result.Errors.Count > 0)
            throw new IOException($"Incomplete scan: {result.Errors.Count} issue{(result.Errors.Count == 1 ? "" : "s")}. Previous index retained. {result.Errors[0]}");
        await index.ReplaceRootAsync(path, result.Files, cancellationToken);
        await ReloadSnapshot();
    }

    private void AutomaticUpdatesChanged(object sender, RoutedEventArgs args)
    {
        if (!loaded || closed) return;
        folderChanges.SetRoots(AutomaticUpdates.IsChecked == true ? roots.Select(r => r.Path) : []);
        Status.Text = AutomaticUpdates.IsChecked == true ? "Automatic folder updates on · Checking connected folders" : "Automatic folder updates paused · Use Refresh library";
    }

    private async void AutomaticTick(object? sender, object args)
    {
        if (closed || busy || previewOpen || AutomaticUpdates.IsChecked != true) return;
        if (DateTime.UtcNow >= nextReconcile)
        {
            nextReconcile = DateTime.UtcNow.AddMinutes(1);
            folderChanges.SetRoots(roots.Select(r => r.Path));
            folderChanges.Reconcile(roots.Select(r => r.Path));
        }
        var changedRoots = folderChanges.TakeReady();
        if (changedRoots.Count == 0) return;
        busy = true;
        using var cancellation = new CancellationTokenSource();
        scanCancellation = cancellation; CancelButton.Visibility = Visibility.Visible;
        var changed = false; var failures = new List<string>(); string? outcome = null;
        try
        {
            foreach (var path in changedRoots)
            {
                if (!roots.Any(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
                try { changed |= await AutomaticRefresh.RefreshAsync(index, path, cancellation.Token, CurrentScanOptions()); }
                catch (IOException ex) { failures.Add(ex.Message); }
            }
            if (failures.Count > 0) outcome = $"Automatic update delayed · {failures[0]}";
            else if (changed) outcome = "Library updated automatically · Index file contents again for changed files";
        }
        catch (OperationCanceledException) { outcome = "Automatic update cancelled · Completed folders saved; next folder check will retry"; }
        catch (Exception ex) { outcome = $"Automatic update failed · {ex.Message}"; }
        finally
        {
            if (changed && !closed)
            {
                try { await ReloadSnapshot(); }
                catch (Exception ex) { outcome = $"Unable to display updated library · {ex.Message}"; }
            }
            scanCancellation = null; busy = false;
            if (!closed) { CancelButton.Visibility = Visibility.Collapsed; if (outcome is not null) Status.Text = outcome; }
        }
    }

    private async void RefreshLibrary(object sender, RoutedEventArgs args)
    {
        if (busy) { Status.Text = "An operation is already running"; return; }
        if (roots.Count == 0) { Status.Text = "Connect a folder first"; return; }
        busy = true;
        using var cancellation = new CancellationTokenSource();
        scanCancellation = cancellation; CancelButton.Visibility = Visibility.Visible;
        var failures = new List<string>();
        try
        {
            foreach (var root in roots.ToArray())
            {
                try { await ScanRoot(root.Path, cancellation.Token); }
                catch (IOException ex) { failures.Add(ex.Message); }
            }
            Status.Text = failures.Count == 0 ? $"Library refreshed · {files.Count:N0} files" : $"{failures.Count} folder{(failures.Count == 1 ? "" : "s")} could not refresh. {failures[0]}";
        }
        catch (OperationCanceledException) { Status.Text = "Refresh cancelled · Completed folders saved; other folders unchanged"; }
        catch (Exception ex) { Status.Text = $"Refresh failed: {ex.Message}"; }
        finally { scanCancellation = null; busy = false; CancelButton.Visibility = Visibility.Collapsed; }
    }

    private async void DisconnectFolder(object sender, RoutedEventArgs args)
    {
        if (busy || RootPicker.SelectedItem is not IndexedRoot root) return;
        busy = true;
        try
        {
            var dialog = new ContentDialog { XamlRoot = Shell.XamlRoot, RequestedTheme = Shell.RequestedTheme, Title = "Disconnect folder?", Content = $"Remove {root.Path} from Ceeker's saved library. Your original files stay untouched.", PrimaryButtonText = "Disconnect", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            await index.RemoveRootAsync(root.Path); await ReloadSnapshot();
            Status.Text = "Folder disconnected · Original files untouched";
        }
        catch (Exception ex) { Status.Text = $"Unable to disconnect folder: {ex.Message}"; }
        finally { busy = false; }
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Windows.UI.Color.FromArgb(255, r, g, b));
    private void UpdateCategories()
    {
        var names = new[] { "Documents", "Images", "Videos", "Downloads", "Music", "Archives", "Apps" };
        var glyphs = new[] { "\uE8A5", "\uEB9F", "\uE714", "\uE896", "\uE8D6", "\uF012", "\uE71D" };
        CategoryGrid.ItemsSource = names.Select((name, index) =>
        {
            var categoryFiles = files.Where(file => name == "Downloads" ? LibraryInsights.IsDownload(file) : file.Category == name).ToArray();
            return new CategoryCard(name, glyphs[index], AppTheme.Brush("CardBrush"), name == "Apps" ? "View installed apps" : $"{FileCount(categoryFiles.Length)} · {LibraryInsights.FormatBytes(categoryFiles.Sum(f => (decimal)f.Length))}");
        }).ToArray();
        LibrarySummary.Text = files.Count == 0 ? "Waiting for your first folder" : $"{files.Count:N0} files · {LibraryInsights.FormatBytes(files.Sum(file => (decimal)file.Length))}";
        HeaderLibrarySize.Text = (testLibrarySession ? "TEST LIBRARY · " : "Library size  ") + LibraryInsights.FormatBytes(files.Sum(file => (decimal)file.Length));
        UpdateInsights();
    }

    private void UpdateInsights()
    {
        SourceRows.Children.Clear();
        var sources = LibraryInsights.Sources(files);
        SourceTotals.Text = $"{sources.Count:N0} source{(sources.Count == 1 ? "" : "s")} across {FileCount(sources.Sum(s => s.Count))} · Showing the {Math.Min(8, sources.Count)} most common. Missing metadata counts as Unknown source.";
        foreach (var source in sources.Take(8))
        {
            var button = InsightCard(source.Name, LibraryInsights.FormatBytes(source.Bytes), FileCount(source.Count), "\uE896", files.Where(f => (f.DownloadSource ?? "Unknown source") == source.Name));
            button.Click += (_, _) => NavigateTo("All files", clearSearch: true, source: source.Name);
            ToolTipService.SetToolTip(button, source.Name);
            SourceRows.Children.Add(button);
        }
        var categories = LibraryInsights.Storage(files);
        var total = categories.Sum(c => c.Bytes);
        StorageTotal.Text = $"{LibraryInsights.FormatBytes(total)} across {FileCount(categories.Sum(c => c.Count))} · logical file sizes in connected folders, not total drive usage.";
        StorageRows.Children.Clear();
        foreach (var category in categories)
        {
            var row = new StackPanel { Spacing = 5 };
            row.Children.Add(new TextBlock { Text = $"{category.Name} · {FileCount(category.Count)} · {LibraryInsights.FormatBytes(category.Bytes)}", FontSize = 13 });
            row.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = total == 0 ? 0 : (double)(category.Bytes / total * 100), Height = 5, Foreground = Brush(115,97,175) });
            StorageRows.Children.Add(row);
        }
        SuggestionRows.Children.Clear();
        var now = DateTime.UtcNow;
        var suggested = CleanupSuggestions.Build(files, preferences.LargeFileMB * 1_000_000L, preferences.DownloadAgeDays, preferences.ScreenshotAgeDays, now);
        CleanupTotals.Text = $"{FileCount(suggested.Count)} to review · {LibraryInsights.FormatBytes(suggested.Sum(s => (decimal)Math.Max(0, s.File.Length)))}\nEach file is counted once across these suggestions. Recycling does not immediately free disk space.";
        var duplicatesCard = InsightCard("Duplicates", dashboardDuplicateGroups?.ToString("N0") ?? "—", "groups · Review comparison", "\uE8C8", []);
        duplicatesCard.Click += (_, _) => NavigateTo("Duplicates"); SuggestionRows.Children.Add(duplicatesCard);
        foreach (var view in new[] { "Old downloads", "Old screenshots", "Large files", "Old installers", "Old videos" })
        {
            var matches = files.Where(f => FileBrowsing.InView(f, view, largeBytes: preferences.LargeFileMB * 1_000_000L, downloadDays: preferences.DownloadAgeDays, screenshotDays: preferences.ScreenshotAgeDays, nowUtc: now)).ToArray();
            var rule = view == "Large files" ? $"at least {preferences.LargeFileMB:N0} MB" : view == "Old installers" ? "modified over 30 days ago; installation status unverified" : view == "Old videos" ? $"at least {preferences.LargeFileMB} MB and modified over 90 days ago" : $"modified more than {(view == "Old downloads" ? preferences.DownloadAgeDays : preferences.ScreenshotAgeDays)} days ago";
            var button = InsightCard(view, LibraryInsights.FormatBytes(matches.Sum(f => (decimal)f.Length)), FileCount(matches.Length), view.Contains("screenshots") ? "\uE722" : "\uE896", matches.OrderByDescending(f => f.ModifiedUtc));
            ToolTipService.SetToolTip(button,rule);
            button.Click += (_, _) => NavigateTo(view, clearSearch: true, source: "All sources");
            SuggestionRows.Children.Add(button);
        }
        var unusedApps = InsightCard("Unused Apps", "Review history", "Recorded launches", "\uE71D", []);
        unusedApps.Click += async (_, _) => { unusedAppsChoice.IsChecked = true; NavigateTo("Apps", clearSearch: true); await ReadApps(); };
        SuggestionRows.Children.Insert(3, unusedApps);
        UpdateDashboard();
    }

    private ScanOptions CurrentScanOptions() => new() { IncludeHiddenFiles = preferences.IncludeHiddenFiles, MaximumFiles = preferences.MaximumScanFiles, ExcludedFolderNames = preferences.ExcludedFolderNames.ToArray() };

    private async void ScanningSettings(object sender, RoutedEventArgs args)
    {
        if (busy || previewOpen) { Status.Text = "Finish the current operation before changing scanning settings"; return; }
        previewOpen = true;
        var refresh = false;
        try
        {
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = "Applies to all connected folders, manual refresh and automatic updates. Original files stay untouched. A failed or file-limited scan retains that folder's previous index.", TextWrapping = TextWrapping.Wrap, MaxWidth = 540 });
            var hidden = new CheckBox { Content = "Include Windows hidden files and folders", IsChecked = preferences.IncludeHiddenFiles };
            var limit = new NumberBox { Header = "Maximum files per connected folder", Minimum = 1, Maximum = 1_000_000, Value = preferences.MaximumScanFiles, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            var exclusions = new TextBox { Header = "Excluded folder names (one per line)", Text = string.Join(Environment.NewLine, preferences.ExcludedFolderNames), AcceptsReturn = true, Height = 110, TextWrapping = TextWrapping.Wrap };
            var common = new Button { Content = "Add common cache/build exclusions", HorizontalAlignment = HorizontalAlignment.Left };
            common.Click += (_, _) => exclusions.Text = string.Join(Environment.NewLine, ScanOptions.NormalizeExclusions(exclusions.Text.Split(['\r', '\n', ',', ';']).Concat([".git", "node_modules", "bin", "obj", ".cache", "__pycache__"])));
            var error = new TextBlock { Foreground = AppTheme.Brush("WarningBrush"), TextWrapping = TextWrapping.Wrap, MaxWidth = 540 };
            panel.Children.Add(hidden); panel.Children.Add(limit); panel.Children.Add(exclusions); panel.Children.Add(common);
            panel.Children.Add(new TextBlock { Text = "Names match nested folders exactly, ignoring case. No paths or wildcards. An explicitly connected root is always scanned, even if its name is excluded. Hidden means the Windows attribute, not a leading dot.", FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 540 });
            panel.Children.Add(error);
            var dialog = new ContentDialog { XamlRoot = Shell.XamlRoot, RequestedTheme = Shell.RequestedTheme, Title = "Scanning settings", Content = panel, PrimaryButtonText = "Save and refresh", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            dialog.PrimaryButtonClick += (_, click) =>
            {
                var names = exclusions.Text.Split(['\r', '\n', ',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var normalized = ScanOptions.NormalizeExclusions(names);
                if (!double.IsFinite(limit.Value) || limit.Value != Math.Truncate(limit.Value) || limit.Value < 1 || limit.Value > 1_000_000 || names.Any(n => !normalized.Contains(n, StringComparer.OrdinalIgnoreCase)))
                { error.Text = "Enter a whole-number limit from 1 to 1,000,000 and up to 100 valid folder names without paths or wildcards."; click.Cancel = true; return; }
                var previous = CurrentScanOptions();
                preferences.IncludeHiddenFiles = hidden.IsChecked == true; preferences.MaximumScanFiles = (int)limit.Value; preferences.ExcludedFolderNames = normalized.ToList();
                try { preferences.Save(preferencesPath); refresh = true; }
                catch (Exception ex)
                {
                    preferences.IncludeHiddenFiles = previous.IncludeHiddenFiles; preferences.MaximumScanFiles = previous.MaximumFiles; preferences.ExcludedFolderNames = previous.ExcludedFolderNames.ToList();
                    error.Text = $"Settings could not save: {ex.Message}"; click.Cancel = true;
                }
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex) { Status.Text = $"Unable to change scanning settings: {ex.Message}"; }
        finally { previewOpen = false; }
        if (refresh) { if (roots.Count > 0) RefreshLibrary(sender, args); else Status.Text = "Scanning settings saved · Connect a folder to apply them"; }
    }

    private async void InsightSettings(object sender, RoutedEventArgs args)
    {
        if (previewOpen) return;
        previewOpen = true;
        try
        {
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = "Review suggestions only. Old downloads are files inside connected folders named Downloads. Ages use last modification, not download or last-opened dates.", TextWrapping = TextWrapping.Wrap });
            var size = new NumberBox { Header = "Large files: minimum MB", Value = preferences.LargeFileMB, Minimum = 1, Maximum = 1_000_000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            var downloads = new NumberBox { Header = "Old downloads: days", Value = preferences.DownloadAgeDays, Minimum = 1, Maximum = 3650, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            var screenshots = new NumberBox { Header = "Old screenshots: days", Value = preferences.ScreenshotAgeDays, Minimum = 1, Maximum = 3650, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            panel.Children.Add(size); panel.Children.Add(downloads); panel.Children.Add(screenshots);
            var dialog = new ContentDialog { XamlRoot = Shell.XamlRoot, RequestedTheme = Shell.RequestedTheme, Title = "Review settings", Content = panel, PrimaryButtonText = "Save", CloseButtonText = "Cancel" };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            if (!double.IsFinite(size.Value) || !double.IsFinite(downloads.Value) || !double.IsFinite(screenshots.Value)) { Status.Text = "Enter a number for each review setting"; return; }
            preferences.LargeFileMB = (int)Math.Clamp(size.Value, 1, 1_000_000);
            preferences.DownloadAgeDays = (int)Math.Clamp(downloads.Value, 1, 3650);
            preferences.ScreenshotAgeDays = (int)Math.Clamp(screenshots.Value, 1, 3650);
            SavePreferences(); UpdateInsights(); UpdateFiles();
        }
        catch (Exception ex) { Status.Text = $"Unable to open review settings: {ex.Message}"; }
        finally { previewOpen = false; }
    }

    private static string FormatSize(long bytes) => bytes >= 1_000_000_000 ? $"{bytes / 1_000_000_000d:F1} GB" : $"{bytes / 1_000_000d:F1} MB";
    private static string FileCount(int count) => $"{count:N0} file{(count == 1 ? "" : "s")}";
    private void Navigate(object sender, RoutedEventArgs args)
    {
        if (sender is not Button button || button.Tag is not string category) return;
        NavigateTo(category, navigation: button);
    }
    private void CategoryClicked(object sender, ItemClickEventArgs args)
    {
        NavigateTo(((CategoryCard)args.ClickedItem).Name);
    }
    private int visibleFileLimit=200;
    private void SearchChanged(object sender, TextChangedEventArgs args) { visibleFileLimit=200; if (!restoringRoute && FileGrid is not null) UpdateFiles(); }
    private void SearchViewChanged(object sender,RoutedEventArgs args) { visibleFileLimit=200; if(!restoringPreferences&&!restoringRoute&&FileGrid is not null) UpdateFiles(); }
    private void ShowMoreFiles(object sender,RoutedEventArgs args) { visibleFileLimit=Math.Min(1000,visibleFileLimit+200); UpdateFiles(); }
    private void ContentScopeChanged(object sender, RoutedEventArgs args)
    {
        if (restoringRoute || FileGrid is null) return;
        if (!restoringPreferences) { preferences.IncludeContents = SearchContents.IsChecked == true; SavePreferences(); }
        UpdateFiles();
    }

    private void AddShortcut(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
    {
        var shortcut = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        shortcut.Invoked += (_, args) => { if (previewOpen) return; args.Handled = true; action(); };
        Shell.KeyboardAccelerators.Add(shortcut);
    }
    private BrowseRoute CurrentRoute() => new(currentCategory, pinnedFolder, SearchBox.Text, SearchContents.IsChecked == true, SourcePicker.SelectedItem as string ?? "All sources", SearchCurrentView.IsChecked == true);
    private void NavigateTo(string category, string? folder = null, bool clearSearch = true, string? source = null, Button? navigation = null)
    {
        visibleFileLimit=200;
        if (previewOpen) return;
        history.ReplaceCurrent(CurrentRoute());
        var current = CurrentRoute();
        var target = new BrowseRoute(category, folder, clearSearch ? "" : current.Query, current.Contents, source ?? (clearSearch ? "All sources" : current.Source), current.CurrentView);
        history.Visit(target); RestoreRoute(target, navigation);
    }
    private void RestoreRoute(BrowseRoute route, Button? navigation = null)
    {
        restoringRoute = true;
        try
        {
            pinnedFolder = route.Folder is not null && roots.Any(r => string.Equals(r.Path, route.Folder, StringComparison.OrdinalIgnoreCase) || FileBrowsing.InFolder(new FileEntry(route.Folder,"",0,DateTime.MinValue),r.Path)) ? route.Folder : null;
            currentCategory = route.Folder is not null && pinnedFolder is null ? "All files" : route.Category;
            SearchCurrentView.IsChecked=route.CurrentView; SearchBox.Text = route.Query; SearchContents.IsChecked = route.Contents;
            SourcePicker.SelectedItem = SourcePicker.Items.Contains(route.Source) ? route.Source : "All sources";
            if (activeNavigation is not null) activeNavigation.Background = AppTheme.Brush("SidebarBrush");
            activeNavigation = navigation ?? FindNavigation(Shell, pinnedFolder ?? currentCategory);
            if (activeNavigation is not null) activeNavigation.Background = AppTheme.Brush("SelectedBrush");
        }
        finally { restoringRoute = false; }
        BackButton.IsEnabled = history.CanBack; ForwardButton.IsEnabled = history.CanForward;
        UpdateFiles(); BrowseScroll.ChangeView(null, 0, null);
    }
    private void GoBack(object sender, RoutedEventArgs args)
    { if (previewOpen || !history.CanBack) return; history.ReplaceCurrent(CurrentRoute()); RestoreRoute(history.Back()); }
    private void GoForward(object sender, RoutedEventArgs args)
    { if (previewOpen || !history.CanForward) return; history.ReplaceCurrent(CurrentRoute()); RestoreRoute(history.Forward()); }
    private static Button? FindNavigation(DependencyObject parent, string tag)
    {
        if (parent is Button button && button.Tag as string == tag) return button;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var found = FindNavigation(VisualTreeHelper.GetChild(parent, i), tag); if (found is not null) return found; }
        return null;
    }

    private static bool SupportsOcr(FileEntry file) => ImageOcr.Supports(file.Extension);

    private static void ValidateContentFile(FileEntry file)
    {
        var info = new FileInfo(file.Path);
        if (!info.Exists || info.Length != file.Length || info.LastWriteTimeUtc != file.ModifiedUtc)
            throw new IOException("File changed. Refresh the library first.");
        for (FileSystemInfo? part = info; part is not null; part = part is FileInfo f ? f.Directory : ((DirectoryInfo)part).Parent)
            if ((part.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0)
                throw new IOException("Linked or offline paths are unsupported.");
    }

    private async void IndexContents(object sender, RoutedEventArgs args)
    {
        if (busy) { Status.Text = "An operation is already running"; return; }
        if (files.Count == 0) { Status.Text = "Connect a folder first"; return; }
        busy = true;
        using var cancellation = new CancellationTokenSource();
        scanCancellation = cancellation; CancelButton.Visibility = Visibility.Visible;
        var snapshot = files.ToArray();
        var indexed = 0; var skipped = 0; var truncated = 0; string? firstError = null;
        try
        {
            foreach (var file in snapshot)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (!DocumentText.Supports(file.Extension) && !SupportsOcr(file)) { skipped++; continue; }
                Status.Text = $"Indexing contents {indexed + skipped + 1:N0}/{snapshot.Length:N0} · {file.Name}";
                try
                {
                    await Task.Run(() => ValidateContentFile(file), cancellation.Token);
                    string text; string source;
                    if (SupportsOcr(file))
                    {
                        var result = await ImageOcr.ReadAsync(file.Path, cancellation.Token);
                        text = result.Text; source = "Image OCR · " + ImageOcr.LanguageDescription;
                        if (text.Length > TextPreview.MaximumCharacters) { text = text[..TextPreview.MaximumCharacters]; truncated++; }
                    }
                    else if (file.Extension == ".pdf")
                    {
                        var progress = new Progress<string>(value => { if (scanCancellation == cancellation) Status.Text = $"{file.Name} · {value}"; });
                        var result = await PdfContent.ReadAsync(file.Path, progress, cancellation.Token);
                        text = result.Text; source = $"PDF text + local OCR ({result.OcrPages} pages)";
                        if (result.Truncated) truncated++;
                    }
                    else
                    {
                        var result = await DocumentText.ReadAsync(file.Path, cancellation.Token);
                        text = result.Text; source = file.Extension switch { ".docx" => "DOCX body", ".pptx" => "PowerPoint slides", ".xlsx" => "Excel saved cell values", ".pdf" => "PDF selectable text", _ => "Text" };
                        if (result.Truncated) truncated++;
                    }
                    await Task.Run(() => ValidateContentFile(file), cancellation.Token);
                    await index.StoreContentAsync(file, text, source, cancellation.Token);
                    indexed++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { skipped++; firstError ??= $"{file.Name}: {ex.Message}"; }
            }
            Status.Text = $"Content index ready · {indexed:N0} indexed · {skipped:N0} skipped · {truncated:N0} truncated" + (firstError is null ? "" : $" · {firstError}");
            RecordActivity($"Content indexed · {indexed:N0} files");
        }
        catch (OperationCanceledException) { Status.Text = $"Content indexing cancelled · {indexed:N0} completed files saved"; }
        catch (Exception ex) { Status.Text = $"Content indexing failed: {ex.Message}"; }
        finally { scanCancellation = null; busy = false; CancelButton.Visibility = Visibility.Collapsed; UpdateFiles(); }
    }
    private void SavePreferences()
    {
        if (restoringPreferences) return;
        try { preferences.Save(preferencesPath); }
        catch (Exception ex) { Status.Text = $"Unable to save view preferences: {ex.Message}"; }
    }
    private void SetBrowseView(bool list)
    {
        FileGrid.Visibility = list ? Visibility.Collapsed : Visibility.Visible;
        FileList.Visibility = list ? Visibility.Visible : Visibility.Collapsed;
        GridViewButton.IsChecked = !list; ListViewButton.IsChecked = list;
        ThumbnailControls.Visibility = list ? Visibility.Collapsed : Visibility.Visible;
    }
    private void ShowGrid(object sender, RoutedEventArgs args) { SetBrowseView(false); preferences.ListView = false; SavePreferences(); }
    private void ShowList(object sender, RoutedEventArgs args) { SetBrowseView(true); preferences.ListView = true; SavePreferences(); }
    private void SourceChanged(object sender, SelectionChangedEventArgs args) { visibleFileLimit=200; if (!restoringRoute && !updatingSourcePicker && FileGrid is not null) UpdateFiles(); }
    private void SortChanged(object sender, SelectionChangedEventArgs args)
    {
        if (restoringPreferences || FileGrid is null) return;
        preferences.Sort = Math.Clamp(SortPicker.SelectedIndex, 0, 7); SavePreferences(); UpdateFiles();
    }
    private void ThumbnailSizeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (restoringPreferences || FileGrid is null) return;
        preferences.TileWidth = args.NewValue;
        if (FileGrid.ItemsSource is IEnumerable<FileCard> cards) foreach (var card in cards) card.Resize(args.NewValue);
        SavePreferences();
    }
    private void PinFolder(object sender, RoutedEventArgs args)
    {
        if (RootPicker.SelectedItem is not IndexedRoot root) { Status.Text = "Select a connected folder first"; return; }
        if (!preferences.PinnedFolders.Contains(root.Path, StringComparer.OrdinalIgnoreCase)) preferences.PinnedFolders.Add(root.Path);
        SavePreferences(); UpdatePinnedFolders();
    }
    private void UnpinFolder(object sender, RoutedEventArgs args)
    {
        if (RootPicker.SelectedItem is not IndexedRoot root) return;
        preferences.PinnedFolders.RemoveAll(p => string.Equals(p, root.Path, StringComparison.OrdinalIgnoreCase));
        SavePreferences(); UpdatePinnedFolders();
    }
    private void UpdatePinnedFolders()
    {
        PinnedPanel.Children.Clear();
        var pinned = roots.Where(r => preferences.PinnedFolders.Contains(r.Path, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (pinned.Length == 0) return;
        PinnedPanel.Children.Add(new TextBlock { Text = "PINNED FOLDERS", FontSize = 10, Margin = new Thickness(16,14,0,4), Foreground = (SolidColorBrush)Shell.Resources["Muted"] });
        foreach (var root in pinned)
        {
            var button = new Button { Content = root.Name, Tag = root.Path, Style = (Style)Shell.Resources["NavButton"] };
            ToolTipService.SetToolTip(button, root.Path);
            button.Click += (_, _) =>
            {
                NavigateTo(root.Name, root.Path, navigation: button);
            };
            if (string.Equals(pinnedFolder, root.Path, StringComparison.OrdinalIgnoreCase)) { activeNavigation = button; button.Background = AppTheme.Brush("SelectedBrush"); }
            PinnedPanel.Children.Add(button);
        }
    }
    private void CancelScan(object sender, RoutedEventArgs args) => scanCancellation?.Cancel();
    private void OpenCleanupReview(object sender, RoutedEventArgs args) => NavigateTo("Cleanup review", clearSearch: true, source: "All sources");
    private void ClearSearch(object sender, RoutedEventArgs args) => SearchBox.Text = "";

    private async void UpdateFiles()
    {
        searchCancellation?.Cancel();
        UpdateFolderHierarchy();
        BottomDashboard.Visibility = currentCategory == "Home" && pinnedFolder is null && string.IsNullOrWhiteSpace(SearchBox.Text) && roots.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var duplicates = currentCategory == "Duplicates" && pinnedFolder is null;
        var similar = currentCategory == "Similar photos" && pinnedFolder is null;
        var apps = currentCategory == "Apps" && pinnedFolder is null;
        appsScroll.Visibility = apps ? Visibility.Visible : Visibility.Collapsed;
        BrowseScroll.Visibility = duplicates || similar || apps ? Visibility.Collapsed : Visibility.Visible;
        DuplicateScroll.Visibility = duplicates ? Visibility.Visible : Visibility.Collapsed;
        SimilarScroll.Visibility = similar ? Visibility.Visible : Visibility.Collapsed;
        SearchBox.IsEnabled = !duplicates && !similar && !apps;
        SearchContents.IsEnabled = !apps;
        if (apps && appSnapshot is null) await ReadApps();
        if (duplicates || similar || apps) return;
        using var cancellation = new CancellationTokenSource();
        searchCancellation = cancellation;
        try
        {
        var searching = !string.IsNullOrWhiteSpace(SearchBox.Text);
        var globalSearch = searching && SearchCurrentView.IsChecked != true;
        var contentScope = SearchContents.IsChecked == true;
        var query = SearchParser.Parse(SearchBox.Text);
        if (preferences.SemanticSearch && !SearchBox.Text.Contains('"')) query.Chips.Insert(0,"Offline AI similarity · up to 1,000 text candidates and 300 photos · approximate");
        var relatedWords = preferences.RelatedWords && !SearchBox.Text.Contains('"');
        if (relatedWords) query.Chips.Insert(0,"Related-word matching · offline synonym list");
        for (var i = 0; i < query.Chips.Count; i++) query.Chips[i] = query.Chips[i].Replace("Name contains", contentScope ? "Name, tag or content term" : "Name or tag contains");
        if (contentScope)
        {
            // Content scope is exposed in Search options; chips describe only parsed filters.
        }
        InterpretationPanel.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        SetFilterChips(globalSearch || pinnedFolder is null && currentCategory is "Home" or "All files" ? query.Chips : new[] { $"Within {pinnedFolder ?? currentCategory}" }.Concat(query.Chips));
        QueryWarning.Text = string.Join(" ", query.Warnings);
        QueryWarning.Visibility = query.Warnings.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        var home = currentCategory == "Home" && pinnedFolder is null && !searching;
        BottomDashboard.Visibility = home && roots.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        BrowseCommands.Visibility = home ? Visibility.Collapsed : Visibility.Visible;
        var storage = currentCategory == "Storage" && pinnedFolder is null;
        InsightsPanel.Visibility = (home && roots.Count > 0 || storage) ? Visibility.Visible : Visibility.Collapsed;
        WelcomePanel.Visibility = roots.Count == 0 && home ? Visibility.Visible : Visibility.Collapsed;
        LibraryPanel.Visibility = home && roots.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SourcesPanel.Visibility = home && roots.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        StorageSection.Visibility = storage ? Visibility.Visible : Visibility.Collapsed;
        FileSection.Visibility = home || storage ? Visibility.Collapsed : Visibility.Visible;
        SearchBox.PlaceholderText = contentScope ? "Search files, folders or indexed text…" : "Search files, folders or tags…";
        PageTitle.Text = searching ? "Search results" : home ? DateTime.Now.Hour < 12 ? "Good morning" : DateTime.Now.Hour < 18 ? "Good afternoon" : "Good evening" : currentCategory;
        PageTitle.FontSize=28;
        PageTitle.FontFamily=new FontFamily("Segoe UI");
        PageSubtitle.Text = pinnedFolder is not null ? pinnedFolder : currentCategory == "Downloaded from" ? "Files with saved download-source metadata. Use the source filter below." : currentCategory == "Large files" ? "Files at least 100 MB in your connected folders." : currentCategory == "Screenshots" ? "Images named Screenshot or Screen shot in your connected folders." : contentScope ? "Search names, tags and saved document text or English OCR. Use Index contents after file changes." : searching ? "Filename, tags, type, size and modification date · entirely on your computer." : home ? $"{files.Count:N0} files · {LibraryInsights.FormatBytes(files.Sum(f => (decimal)f.Length))} across your connected folders" : "A focused view of your library.";
        if (currentCategory == "Large files") PageSubtitle.Text = $"Files at least {preferences.LargeFileMB:N0} MB. Adjust the threshold in Review settings.";
        if (currentCategory == "Downloads") PageSubtitle.Text = "Files inside folders named Downloads, within your connected library.";
        if (currentCategory == "Cleanup review") PageSubtitle.Text = "Large files, old downloads, screenshots, installers and videos; overlaps counted once. Installer installation status is unverified. Review before recycling.";
        if (currentCategory == "Old installers") PageSubtitle.Text = "Windows installer packages and setup/installer-named EXEs modified over 30 days ago. This does not prove they were installed or are no longer needed.";
        if (currentCategory == "Old videos") PageSubtitle.Text = $"Videos at least {preferences.LargeFileMB} MB, modified over 90 days ago. Review originals before recycling.";
        if (currentCategory is "Old downloads" or "Old screenshots") PageSubtitle.Text = $"Modified more than {(currentCategory == "Old downloads" ? preferences.DownloadAgeDays : preferences.ScreenshotAgeDays)} days ago. {(currentCategory == "Old downloads" ? "Only connected paths within folders named Downloads." : "Images named Screenshot or Screen shot.")} Review before deciding what to keep.";
        if (storage) { PageTitle.Text = "Storage & review"; PageSubtitle.Text = "Understand your connected library and explore files worth reviewing."; InterpretationPanel.Visibility = Visibility.Collapsed; }
        FilesTitle.Text = searching ? "Search results" : home ? preferences.Sort == 0 ? "Recent files" : "Your files" : currentCategory;
        FilesTitle.Visibility = home ? Visibility.Visible : Visibility.Collapsed;
        if(searching) PageSubtitle.Text = globalSearch ? "Search across all connected folders · names, tags and indexed text." : "Search restricted to this view · clear Search this view to search your whole library.";
        if (!home && !searching && !storage) PageSubtitle.Text = "";
        var snapshot = await DeviceSearchSnapshotAsync(files, query, cancellation.Token);
        var category = globalSearch ? "All files" : currentCategory;
        var folder = globalSearch ? null : pinnedFolder;
        var sort = (FileSort)preferences.Sort;
        var sourceFilter = SourcePicker.SelectedItem as string ?? "All sources";
        var largeBytes = preferences.LargeFileMB * 1_000_000L;
        var downloadDays = preferences.DownloadAgeDays;
        var screenshotDays = preferences.ScreenshotAgeDays;
        var nowUtc = DateTime.UtcNow;
        if (searching) await Task.Delay(180, cancellation.Token);
        var semanticCandidates = preferences.SemanticSearch && query.Terms.Count>0 && !SearchBox.Text.Contains('"') ? await Task.Run(()=>snapshot.Where(file=>FileBrowsing.InView(file,category,folder,largeBytes,downloadDays,screenshotDays,nowUtc) && (sourceFilter=="All sources" || sourceFilter=="Unknown source" && file.DownloadSource is null || string.Equals(file.DownloadSource,sourceFilter,StringComparison.OrdinalIgnoreCase))).ToArray(),cancellation.Token) : Array.Empty<FileEntry>();
        if(preferences.SemanticSearch&&query.Terms.Count>0&&!SearchBox.Text.Contains('"'))
        {
            syncingSelection=true; selectedPaths.Clear(); FileGrid.ItemsSource=Array.Empty<FileCard>(); FileList.ItemsSource=Array.Empty<FileCard>(); syncingSelection=false; SyncSelection(); EmptyPanel.Visibility=Visibility.Collapsed; ResultCount.Text="Searching offline…";
        }
        var semanticMatches = await SemanticMatchesAsync(query,semanticCandidates,cancellation.Token);
        var contentPaths = contentScope && searching && query.Terms.Count>0 ? await index.SearchContentAsync(query.Terms, cancellation.Token, relatedWords) : null;
        var savedTags = await index.LoadTagsAsync(cancellation.Token);
        var matches = await Task.Run(() => FileBrowsing.Sort(snapshot.Where(file =>
        {
            cancellation.Token.ThrowIfCancellationRequested();
            return FileBrowsing.InView(file, category, folder, largeBytes, downloadDays, screenshotDays, nowUtc) &&
                (folder is null || searching || string.Equals(System.IO.Path.GetDirectoryName(file.Path),System.IO.Path.TrimEndingDirectorySeparator(folder),StringComparison.OrdinalIgnoreCase)) &&
                (sourceFilter == "All sources" || sourceFilter == "Unknown source" && file.DownloadSource is null || string.Equals(file.DownloadSource, sourceFilter, StringComparison.OrdinalIgnoreCase)) &&
                (FileSearch.Matches(query, file, savedTags.TryGetValue(file.Path, out var tags) ? tags : [], contentPaths?.Contains(file.Path) == true, relatedWords) || semanticMatches.ContainsKey(file.Path));
        }), sort).ToArray(), cancellation.Token);
        cancellation.Token.ThrowIfCancellationRequested();
        var limit = home ? 12 : visibleFileLimit;
        ShowMoreFilesButton.Visibility = !home && matches.Length>limit && limit<1000 ? Visibility.Visible : Visibility.Collapsed;
        if (preferences.SemanticSearch && semanticMatches.Count > 0) matches = matches.OrderByDescending(file=>semanticMatches.GetValueOrDefault(file.Path)).ToArray();
        var largestMatch = matches.Select(file => Math.Max(0, file.Length)).DefaultIfEmpty(0).Max();
        if (pinnedFolder is not null) PageSubtitle.Text = $"{matches.Length:N0} matching files · {LibraryInsights.FormatBytes(matches.Sum(file=>(decimal)file.Length))} logical size";
        var visible = matches.Take(limit).Select(file => new FileCard(file, preferences.TileWidth)
        {
            SizePercent = largestMatch == 0 ? 0 : (double)((decimal)Math.Max(0, file.Length) / largestMatch * 100),
            SizeBarVisibility = category == "Large files" ? Visibility.Visible : Visibility.Collapsed,
            ReviewReason = category == "Cleanup review" ? string.Join(" · ", CleanupSuggestions.Build([file], largeBytes, downloadDays, screenshotDays, nowUtc).Single().Reasons) : ""
        }).ToArray();
        syncingSelection = true;
        FileGrid.ItemsSource = visible;
        FileList.ItemsSource = visible; ApplyResponsiveLayout();
        selectedPaths.IntersectWith(visible.Select(c => c.Path));
        syncingSelection = false;
        SyncSelection();
        var sortLabel = ((ComboBoxItem)SortPicker.Items[(int)sort]).Content.ToString()!.ToLowerInvariant();
        ResultCount.Text = matches.Length == 0 ? searching ? "No matching files" : "No files here yet" : matches.Length <= limit ? $"{FileCount(matches.Length)} · {sortLabel}" : $"Showing {visible.Length:N0} of {FileCount(matches.Length)} · {sortLabel}";
        EmptyPanel.Visibility = matches.Length == 0 && WelcomePanel.Visibility != Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = searching ? "No matches for this search" : roots.Count == 0 ? "Connect your first folder" : files.Count == 0 ? "This library is empty" : contentScope ? "No indexed content here yet" : $"No {currentCategory.ToLowerInvariant()} here yet";
        EmptyDescription.Text = roots.Count == 0 ? "Choose Connect folder to start browsing your files." : searching && query.Terms.Count==0 ? "No connected files match these filters. Try a wider date range; dates normally use last modification time." : contentScope ? "Choose Index contents to make file text searchable, or try a different word or phrase." : searching ? "Try fewer words, clear a size or date filter, or search another category." : files.Count == 0 ? "Add files to a connected folder. Automatic updates will pick them up while enabled." : "Choose All files or connect another folder to browse more files.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Status.Text = $"Search failed: {ex.Message}"; }
        finally { if (searchCancellation == cancellation) searchCancellation = null; }
    }

    private async void ScanDuplicates(object sender, RoutedEventArgs args)
        => await RunDuplicateScanAsync(false);

    private async Task RunDuplicateScanAsync(bool automatic)
    {
        if (busy) { Status.Text = "An operation is already running"; return; }
        var folders = automatic ? null : duplicateFolders;
        var scanFiles = DuplicateScanScope.Select(files, folders);
        if(scanFiles.Length==0){DuplicateSummary.Text="No indexed files in the selected scan folders. Refresh or choose another folder.";return;}
        DuplicateScopeText.Text=DescribeDuplicateScope(folders)+(automatic ? " · Automatic scan" : "");
        automaticDuplicatesPending = false;
        busy = true;
        using var cancellation = new CancellationTokenSource();
        if (automatic) automaticDuplicateCancellation = cancellation;
        scanCancellation = cancellation;
        DuplicateScanButton.IsEnabled = false; DuplicateFoldersButton.IsEnabled=false; DuplicateDisconnectButton.IsEnabled=false;
        CancelButton.Visibility = Visibility.Visible;
        DuplicateProgressBar.Visibility = Visibility.Visible;
        ClearDuplicates();
        DuplicateSummary.Text = "Comparing file contents… Matches will appear as they are found.";
        var comparisonFinished = false;
        var foundGroups = 0;
        try
        {
            var progress = new Progress<DuplicateProgress>(value =>
            {
                if (scanCancellation != cancellation || comparisonFinished) return;
                DuplicateProgressBar.Maximum = Math.Max(1, value.Candidates);
                DuplicateProgressBar.Value = value.Completed;
                if (value.FoundGroup is { } found && foundGroups < 100) { RenderDuplicateGroup(found); foundGroups++; }
                var currentFile = value.FileName is null ? "" : $" · {value.FileName}: {LibraryInsights.FormatBytes(value.FileBytes)} / {LibraryInsights.FormatBytes(value.FileLength)}";
                DuplicateSummary.Text = $"Comparing {value.Completed:N0} of {value.Candidates:N0} candidate files · {foundGroups:N0} groups shown so far{currentFile}";
                Status.Text = $"Compared {value.Completed:N0} of {value.Candidates:N0} candidate files";
            });
            var result = await DuplicateScanner.ScanAsync(scanFiles, progress, cancellation.Token);
            comparisonFinished = true;
            duplicateReview.Complete();
            duplicateSelections.Clear(); DuplicateGroupsPanel.Children.Clear();
            dashboardDuplicateGroups = result.Groups.Count;
            UpdateDashboard();
            RecordActivity($"Duplicate scan complete · {result.Groups.Count:N0} groups");
            var bytes = result.Groups.Sum(group => (decimal)group.Files[0].Length * (group.Files.Count - 1));
            DuplicateSummary.Text = $"{result.Groups.Count:N0} duplicate groups · {result.Groups.Sum(group => group.Files.Count - 1):N0} extra copies · {bytes / 1_000_000m:F2} MB logical duplicate bytes. {result.Skipped.Count:N0} files skipped.";
            foreach (var group in result.Groups.Take(100)) RenderDuplicateGroup(group);
            if (result.Groups.Count > 100) DuplicateSummary.Text += " Showing the first 100 groups.";
            if (result.Skipped.Count > 0)
                DuplicateGroupsPanel.Children.Add(new TextBlock { Text = "Skipped files (first 10):\n" + string.Join("\n", result.Skipped.Take(10)), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            if (result.Groups.Count == 0)
                DuplicateGroupsPanel.Children.Insert(0, new TextBlock { Text = result.Skipped.Count > 0
                    ? "No exact duplicates found among the files successfully compared. Some files could not be checked; refresh your library and review the skipped-file details."
                    : "No exact duplicates found. This scan compares identical file contents, not similar photos or matching filenames.", TextWrapping = TextWrapping.Wrap });
            Status.Text = "Duplicate review complete · No files modified";
        }
        catch (OperationCanceledException)
        {
            DuplicateSummary.Text = "Scan cancelled. Run again for complete results.";
            Status.Text = "Duplicate scan cancelled";
            if (automatic && !closed) { preferences.AutomaticDuplicates = false; automaticDuplicatesChoice.IsChecked = false; SavePreferences(); Status.Text = "Automatic duplicate scanning paused · Manual scan remains available"; }
        }
        catch (Exception ex) { DuplicateSummary.Text = $"Unable to compare files: {ex.Message}"; }
        finally
        {
            comparisonFinished = true;
            automaticDuplicateCancellation = null;
            scanCancellation = null; busy = false;
            DuplicateScanButton.IsEnabled = true; DuplicateFoldersButton.IsEnabled=true; DuplicateDisconnectButton.IsEnabled=true;
            CancelButton.Visibility = Visibility.Collapsed;
            DuplicateProgressBar.Visibility = Visibility.Collapsed;
            UpdateCleanupButton();
        }
    }

    private void RenderDuplicateGroup(DuplicateGroup group)
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = $"{group.Files.Count} matching files · {group.Files[0].DisplaySize} each", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var groupName = Guid.NewGuid().ToString();
        var selection = new DuplicateSelection(group);
        duplicateSelections.Add(selection);
        foreach (var file in group.Files)
        {
            var row = new StackPanel { Spacing = 6 };
            row.Children.Add(new TextBlock { Text = file.Name, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            var controls = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemWidth=180,ItemHeight=48,MaximumRowsOrColumns=4 };
            var preferred = new RadioButton { Content = "Preferred copy", GroupName = groupName, IsChecked = file == group.Files[0] };
            var remove = new CheckBox { Content = "Recycle this copy", IsEnabled = file != selection.Keep };
            selection.Choices.Add(file, remove);
            ToolTipService.SetToolTip(preferred,"Recommendation: prefer outside Downloads, then oldest creation date. You can choose any copy.");
            preferred.Checked += (_, _) =>
            {
                selection.Keep = file;
                foreach (var pair in selection.Choices)
                {
                    pair.Value.IsEnabled = pair.Key != file;
                    if (pair.Key == file) pair.Value.IsChecked = false;
                }
                UpdateCleanupButton();
            };
            remove.Checked += (_, _) => UpdateCleanupButton();
            remove.Unchecked += (_, _) => UpdateCleanupButton();
            controls.Children.Add(preferred);
            controls.Children.Add(remove);
            var preview = new Button { Content = "Preview", CornerRadius = new CornerRadius(8) };
            preview.Click += async (_, _) => await ShowPreview(file);
            controls.Children.Add(preview);
            var copyPath = new Button { Content = "Copy path", CornerRadius = new CornerRadius(8) };
            copyPath.Click += (_, _) => { try { Status.Text = FileActions.CopyPath(file.Path); } catch (Exception ex) { Status.Text = $"Unable to copy path: {ex.Message}"; } };
            controls.Children.Add(copyPath); row.Children.Add(controls); panel.Children.Add(row);
        }
        DuplicateGroupsPanel.Children.Add(new Border { Child = panel, Background = AppTheme.Brush("CardBrush"), BorderBrush = AppTheme.Brush("LineBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(20) });
    }

    private async void ReviewCleanup(object sender, RoutedEventArgs args)
    {
        if (busy || previewOpen) return;
        var choices = duplicateSelections.SelectMany(selection => selection.Choices
            .Where(pair => pair.Key != selection.Keep && pair.Value.IsChecked == true)
            .Select(pair => new CleanupChoice(selection.Keep, pair.Key))).ToArray();
        if (choices.Length == 0) return;
        busy = true; CleanupButton.IsEnabled = false;
        try
        {
            var details = new TextBlock
            {
                Text = "Contents will be checked again before recycling. Restore copies through Windows Recycle Bin. Disk space is not freed immediately.\n\n" +
                    string.Join("\n\n", choices.Select(choice => $"Recycle: {choice.Remove.Path}\nKeep: {choice.Keep.Path}")),
                TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true
            };
            var dialog = new ContentDialog
            {
                XamlRoot = Shell.XamlRoot, RequestedTheme = Shell.RequestedTheme,
                Title = $"Recycle {choices.Length} selected copies?",
                Content = new ScrollViewer { Content = details, MaxHeight = 400 },
                PrimaryButtonText = "Move to Recycle Bin", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            using var cancellation = new CancellationTokenSource();
            scanCancellation = cancellation;
            CancelButton.Visibility = Visibility.Visible;
            DuplicateScanButton.IsEnabled = false; DuplicateFoldersButton.IsEnabled=false; DuplicateDisconnectButton.IsEnabled=false;
            DuplicateGroupsPanel.IsHitTestVisible = false;
            var recycled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var failures = new List<string>();
            try
            {
                foreach (var choice in choices)
                {
                    if (cancellation.IsCancellationRequested) break;
                    Status.Text = $"Rechecking and recycling {recycled.Count + failures.Count + 1} of {choices.Length}…";
                    try
                    {
                        // Hashing is asynchronous; shell operations run on the window's STA thread.
                        await DuplicateCleanup.RevalidateAndRemoveAsync(choice, RecycleBin.Move, cancellation.Token);
                        recycled.Add(choice.Remove.Path);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex) { failures.Add($"{choice.Remove.Path}: {ex.Message}"); }
                }
                files = files.Where(file => !recycled.Contains(file.Path)).ToArray();
                string? metadataWarning = null;
                try { await index.RemoveFilesAsync(recycled); }
                catch (Exception ex) { metadataWarning = $"Saved metadata could not update; refresh library: {ex.Message}"; }
                if(recycled.Count>0) await AnimateCleanupAsync();
                UpdateCategories(); UpdateFiles(); RetainDuplicateReviewAfterRecycle(recycled);
                DuplicateSummary.Text = $"{recycled.Count} copies recycled · {failures.Count} failed · {choices.Length - recycled.Count - failures.Count} not processed. Remaining duplicate groups stay available for review.";
                if (metadataWarning is not null) DuplicateSummary.Text += " " + metadataWarning;
                foreach (var failure in failures.Take(10)) DuplicateGroupsPanel.Children.Add(new TextBlock { Text = failure, TextWrapping = TextWrapping.Wrap });
                Status.Text = "Cleanup finished · Retained copies preserved";
            }
            finally
            {
                scanCancellation = null;
                CancelButton.Visibility = Visibility.Collapsed;
                DuplicateScanButton.IsEnabled = true; DuplicateFoldersButton.IsEnabled=true; DuplicateDisconnectButton.IsEnabled=true;
                DuplicateGroupsPanel.IsHitTestVisible = true;
            }
        }
        catch (Exception ex) { Status.Text = $"Cleanup failed: {ex.Message}"; }
        finally { busy = false; UpdateCleanupButton(); }
    }

    private FileEntry[] SelectedFiles() => (FileGrid.ItemsSource as IEnumerable<FileCard> ?? []).Where(c => selectedPaths.Contains(c.Path)).Select(c => c.Entry).ToArray();

    private void SyncSelection(ListViewBase? origin = null)
    {
        if (SelectionBar is null) return;
        syncingSelection = true;
        foreach (var view in new ListViewBase[] { FileGrid, FileList })
        {
            if (view == origin) continue;
            view.SelectedItems.Clear();
            foreach (var card in view.Items.Cast<FileCard>().Where(c => selectedPaths.Contains(c.Path))) view.SelectedItems.Add(card);
        }
        syncingSelection = false;
        var selected = SelectedFiles();
        SelectionBar.Visibility = selected.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        SelectionSummary.Text = $"{selected.Length:N0} selected · {LibraryInsights.FormatBytes(selected.Sum(f => (decimal)f.Length))}";
        BatchOcrButton.IsEnabled = !busy && selected.Any(SupportsOcr);
        RecycleSelectedButton.IsEnabled = !busy && selected.Length is > 0 and <= 100;
    }

    private void FilesSelected(object sender, SelectionChangedEventArgs args)
    {
        if (syncingSelection) return;
        foreach (var card in args.RemovedItems.OfType<FileCard>()) selectedPaths.Remove(card.Path);
        foreach (var card in args.AddedItems.OfType<FileCard>()) selectedPaths.Add(card.Path);
        SyncSelection(sender as ListViewBase);
    }
    private void SelectAllShown(object sender, RoutedEventArgs args)
    {
        foreach (var card in FileGrid.Items.Cast<FileCard>()) selectedPaths.Add(card.Path);
        SyncSelection();
    }
    private void ClearSelection(object sender, RoutedEventArgs args) { selectedPaths.Clear(); SyncSelection(); }
    private async void PreviewSelection(object sender, RoutedEventArgs args)
    {
        var selected = SelectedFiles();
        if (selected.Length > 0) await ShowPreview(selected[0], selected);
    }
    private async void FileDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    {
        if (sender is not ListViewBase view || view.SelectedItem is not FileCard card) return;
        args.Handled = true;
        await ShowPreview(card.Entry, SelectedFiles());
    }
    private void CopySelectedPaths(object sender, RoutedEventArgs args)
    {
        try { Status.Text = FileActions.CopyPaths(SelectedFiles().Select(f => f.Path)); }
        catch (Exception ex) { Status.Text = $"Unable to copy paths: {ex.Message}"; }
    }
    private async void OpenSelectedFiles(object sender,RoutedEventArgs args)
    {
        if(sender is not Button button || busy || previewOpen) return;
        button.IsEnabled=false;
        try { Status.Text=await FileActions.OpenManyAsync(SelectedFiles().Select(file=>file.Path)); }
        finally { button.IsEnabled=true; }
    }
    private async void RevealSelectedFiles(object sender, RoutedEventArgs args)
    {
        if (sender is not Button button) return;
        button.IsEnabled = false;
        try { Status.Text = await FileActions.RevealManyAsync(SelectedFiles().Select(file => file.Path)); }
        catch (Exception ex) { Status.Text = $"Unable to show files: {ex.Message}"; }
        finally { button.IsEnabled = true; }
    }
    private async void CopySelectedImageText(object sender, RoutedEventArgs args)
    {
        if (busy) return;
        var images = SelectedFiles().Where(SupportsOcr).ToArray();
        if (images.Length == 0) return;
        busy = true; SyncSelection();
        using var cancellation = new CancellationTokenSource();
        scanCancellation = cancellation; CancelButton.Content = "Cancel copy text"; CancelButton.Visibility = Visibility.Visible;
        var text = new System.Text.StringBuilder();
        var copied = 0; var failed = 0; var truncated = false;
        try
        {
            for (var i = 0; i < images.Length; i++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                Status.Text = $"Reading English image text · {i + 1} of {images.Length}";
                try
                {
                    await Task.Run(() => ValidateContentFile(images[i]), cancellation.Token);
                    var result = await ImageOcr.ReadAsync(images[i].Path, cancellation.Token);
                    await Task.Run(() => ValidateContentFile(images[i]), cancellation.Token);
                    if (string.IsNullOrWhiteSpace(result.Text)) { failed++; continue; }
                    var part = $"{images[i].Name}\n{result.Text}\n\n";
                    var remaining = 256_000 - text.Length;
                    if (part.Length > remaining) { text.Append(part.AsSpan(0, remaining)); truncated = true; copied++; break; }
                    text.Append(part); copied++;
                }
                catch (OperationCanceledException) { throw; }
                catch { failed++; }
            }
            cancellation.Token.ThrowIfCancellationRequested();
            if (copied > 0) FileActions.CopyText(text.ToString());
            Status.Text = copied == 0 ? $"No readable text · {failed} empty or unavailable images · Clipboard unchanged" : $"Text copied from {copied} images · {failed} empty or unavailable" + (truncated ? " · 256,000-character limit reached; remaining images not processed" : "");
        }
        catch (OperationCanceledException) { Status.Text = "Text copy cancelled · Clipboard unchanged"; }
        catch (Exception ex) { Status.Text = $"Unable to copy image text: {ex.Message}"; }
        finally { scanCancellation = null; CancelButton.Visibility = Visibility.Collapsed; CancelButton.Content = "Cancel scan"; busy = false; SyncSelection(); }
    }
    private void DragFiles(object sender, DragItemsStartingEventArgs args)
    {
        var entries = args.Items.OfType<FileCard>().Select(c => c.Entry).ToArray();
        if (entries.Length == 0 || previewOpen) { args.Cancel = true; return; }
        FileTransfer.Configure(args.Data, entries, ValidateContentFile, message => DispatcherQueue.TryEnqueue(() => { if (!closed) Status.Text = $"Unable to drag files: {message}"; }));
    }

    private async void PreviewSelected(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var view = FileList.Visibility == Visibility.Visible ? (ListViewBase)FileList : FileGrid;
        if (view.SelectedItem is not FileCard card || previewOpen) return;
        var focused = FocusManager.GetFocusedElement(Shell.XamlRoot) as DependencyObject;
        while (focused is not null && focused != view) focused = VisualTreeHelper.GetParent(focused);
        if (focused != view) return;
        args.Handled = true;
        await ShowPreview(card.Entry, SelectedFiles());
    }

    private void FileContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (previewOpen || sender is not ListViewBase view) return;
        FileCard? card = null;
        for (var element = args.OriginalSource as DependencyObject; element is not null && element != view; element = VisualTreeHelper.GetParent(element))
            if (element is FrameworkElement { DataContext: FileCard found }) { card = found; break; }
        if (card is null && !args.TryGetPosition(view, out _)) card = view.SelectedItem as FileCard;
        if (card is null) return;
        if (!selectedPaths.Contains(card.Path)) { selectedPaths.Clear(); selectedPaths.Add(card.Path); SyncSelection(); }
        var file = card.Entry;
        var presenterStyle = new Style { TargetType = typeof(MenuFlyoutPresenter) };
        presenterStyle.Setters.Add(new Setter(FrameworkElement.RequestedThemeProperty, Shell.RequestedTheme));
        presenterStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brush(255,255,255)));
        presenterStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush(48,48,63)));
        presenterStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brush(228,228,237)));
        presenterStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        var menu = new MenuFlyout();
        var preview = new MenuFlyoutItem { Text = "Preview & file details" };
        preview.Click += async (_, _) => await ShowPreview(file, SelectedFiles());
        var open = new MenuFlyoutItem { Text = "Open in default app" };
        open.Click += async (_, _) => { try { Status.Text = await FileActions.OpenAsync(file.Path); } catch (Exception ex) { Status.Text = $"Unable to open file: {ex.Message}"; } };
        var reveal = new MenuFlyoutItem { Text = "Show in Explorer" };
        reveal.Click += (_, _) => { try { Status.Text = FileActions.Reveal(file.Path); } catch (Exception ex) { Status.Text = $"Unable to reveal file: {ex.Message}"; } };
        var copy = new MenuFlyoutItem { Text = "Copy path" };
        copy.Click += (_, _) => { try { Status.Text = FileActions.CopyPath(file.Path); } catch (Exception ex) { Status.Text = $"Unable to copy path: {ex.Message}"; } };
        menu.Items.Add(preview); menu.Items.Add(open); menu.Items.Add(reveal); menu.Items.Add(copy);
        var editTags = new MenuFlyoutItem { Text = "Edit tags…" }; editTags.Click += async (_, _) => await EditTagsAsync(file); menu.Items.Add(editTags);
        var target = view.ContainerFromItem(card) as FrameworkElement ?? view;
        if (args.TryGetPosition(target, out var point)) menu.ShowAt(target, point); else menu.ShowAt(target);
        args.Handled = true;
    }

    private async Task ShowPreview(FileEntry file, IEnumerable<FileEntry>? selection = null)
    {
        if (previewOpen) return;
        previewOpen = true;
        var visible = (FileGrid.ItemsSource as IEnumerable<FileCard>)?.Select(c => c.Entry) ?? [];
        var selectedSequence=selection?.ToArray();
        var sequence = new PreviewSequence(selectedSequence is { Length:>1 } ? selectedSequence : visible, file);
        var preview = new FilePreviewPane(sequence.Current);
        currentPreviewPane = preview;
        var content = new StackPanel { Spacing = 12 };
        var navigation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var previousFile = new Button { Content = "Previous file" };
        var nextFile = new Button { Content = "Next file" };
        var position = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        navigation.Children.Add(previousFile); navigation.Children.Add(position); navigation.Children.Add(nextFile);
        content.Children.Add(navigation);
        content.Children.Add(new TextBlock { Text = "Left / Right: previous / next · Up / Down: grid row · Alt+arrows: media-safe navigation · Esc: close", FontSize = 11, Foreground = AppTheme.Brush("MutedBrush") });
        content.Children.Add(preview);
        var dialog = new ContentDialog
        {
            XamlRoot = Shell.XamlRoot, Title = file.Name, CloseButtonText = "Close", RequestedTheme = Shell.RequestedTheme,
            Content = new ScrollViewer { Content = content, MaxHeight = Math.Max(180, Shell.XamlRoot.Size.Height - 220), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        void UpdateNavigation()
        {
            previousFile.IsEnabled = sequence.CanPrevious; nextFile.IsEnabled = sequence.CanNext;
            position.Text = $"{sequence.Position + 1} of {sequence.Count} shown files";
        }
        void Move(int delta)
        {
            if (!sequence.Move(delta)) return;
            preview.Close(); content.Children.Remove(preview);
            preview = new FilePreviewPane(sequence.Current); currentPreviewPane = preview; content.Children.Add(preview);
            dialog.Title = sequence.Current.Name; UpdateNavigation();
        }
        previousFile.Click += (_, _) => Move(-1); nextFile.Click += (_, _) => Move(1);
        content.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, args) =>
        {
            if (args.Key is not (VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down)) return;
            var alt = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)&Windows.UI.Core.CoreVirtualKeyStates.Down)!=0;
            var focus=FocusManager.GetFocusedElement(Shell.XamlRoot) as DependencyObject;
            for(var node=focus;node is not null;node=VisualTreeHelper.GetParent(node))
                if(!alt && (node is TextBox or Slider or ComboBox or MediaPlayerElement || node is TextBlock { IsTextSelectionEnabled:true } || sequence.Current.Category is "Music" or "Videos"))return;
            var columns=preferences.ListView?1:Math.Max(1,(int)(FileGrid.ActualWidth/Math.Max(160,preferences.TileWidth+8)));
            Move(args.Key switch { VirtualKey.Left=>-1,VirtualKey.Right=>1,VirtualKey.Up=>-columns,_=>columns }); args.Handled = true;
        }), true);
        UpdateNavigation();
        dialog.Resources["ContentDialogMaxWidth"] = Math.Min(760d,Math.Max(300,Shell.ActualWidth-60));
        try { await dialog.ShowAsync(); }
        catch (Exception ex) { Status.Text = $"Unable to open preview: {ex.Message}"; }
        finally { preview.Close(); currentPreviewPane = null; previewOpen = false; }
    }

    private async void ChooseFolder(object sender, RoutedEventArgs args)
    {
        if (busy) { Status.Text = "An operation is already running"; return; }
        busy = true;
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return;
            var cancellation = new CancellationTokenSource();
            scanCancellation = cancellation;
            Status.Text = $"Scanning {folder.Path}…";
            CancelButton.Visibility = Visibility.Visible;
            try
            {
                await ScanRoot(folder.Path, cancellation.Token);
                Status.Text = $"Library saved · {files.Count:N0} files in {roots.Count} folders";
            }
            catch (OperationCanceledException)
            { if (scanCancellation == cancellation) Status.Text = "Scan cancelled. Previous results retained."; }
            finally
            {
                cancellation.Dispose();
                if (scanCancellation == cancellation) { scanCancellation = null; CancelButton.Visibility = Visibility.Collapsed; }
            }
        }
        catch (Exception ex) { Status.Text = $"Unable to browse folder: {ex.Message}"; }
        finally { busy = false; }
    }
}













