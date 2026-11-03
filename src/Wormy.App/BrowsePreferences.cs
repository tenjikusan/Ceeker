using System.Text.Json;

namespace Wormy.App;

public sealed class BrowsePreferences
{
    public List<Wormy.Core.SavedSearchDefinition> SavedSearches { get; set; } = [];
    public bool MinimalStyle { get; set; }
    public bool TrayDownloads { get; set; } = true;
    public bool GlobalQuickSearch { get; set; } = true;
    public uint QuickSearchModifiers { get; set; } = 6;
    public uint QuickSearchKey { get; set; } = 32;
    public string RuleFolder { get; set; } = "";
    public bool RuleRecycleInstallers { get; set; }
    public bool RuleOrganizeScreenshots { get; set; }
    public int RuleInstallerAgeDays { get; set; } = 90;
    public int Sort { get; set; }
    public double TileWidth { get; set; } = 180;
    public bool ListView { get; set; }
    public bool IncludeContents { get; set; } = true;
    public bool RelatedWords { get; set; }
    public bool SemanticSearch { get; set; }
    public bool ObserveAppUsage { get; set; }
    public bool DarkTheme { get; set; }
    public bool FollowSystemTheme { get; set; }
    public bool TouchMode { get; set; }
    public bool BackgroundOcr { get; set; }
    public string OcrLanguage { get; set; } = "";
    public bool AutomaticDuplicates { get; set; }
    public bool GettingStartedCompleted { get; set; }
    public string UpdateManifestUrl { get; set; } = "";
    public string UpdatePublisherFingerprint { get; set; } = "";
    public bool InstallSignedUpdates { get; set; }
    public bool IncludeHiddenFiles { get; set; } = true;
    public int MaximumScanFiles { get; set; } = 100_000;
    public List<string> ExcludedFolderNames { get; set; } = [];
    public int LargeFileMB { get; set; } = 100;
    public int DownloadAgeDays { get; set; } = 30;
    public int ScreenshotAgeDays { get; set; } = 30;
    public List<string> PinnedFolders { get; set; } = [];

    public static BrowsePreferences Load(string path)
    {
        if (!File.Exists(path)) return new();
        var value = JsonSerializer.Deserialize<BrowsePreferences>(File.ReadAllText(path)) ?? new();
        value.SavedSearches = Wormy.Core.DesktopFeatures.NormalizeSearches(value.SavedSearches);
        value.RuleFolder ??= ""; value.RuleInstallerAgeDays = Math.Clamp(value.RuleInstallerAgeDays,30,3650);
        if(value.QuickSearchKey is < 32 or > 123 || (value.QuickSearchModifiers & ~7u)!=0 || value.QuickSearchModifiers==0) { value.QuickSearchKey=32;value.QuickSearchModifiers=6; }
        value.Sort = Math.Clamp(value.Sort, 0, 7);
        value.UpdateManifestUrl ??= "";
        value.UpdatePublisherFingerprint ??= "";
        value.OcrLanguage ??= "";
        value.MaximumScanFiles = Math.Clamp(value.MaximumScanFiles, 1, 1_000_000);
        value.ExcludedFolderNames = Wormy.Core.ScanOptions.NormalizeExclusions(value.ExcludedFolderNames).ToList();
        value.LargeFileMB = Math.Clamp(value.LargeFileMB, 1, 1_000_000);
        value.DownloadAgeDays = Math.Clamp(value.DownloadAgeDays, 1, 3650);
        value.ScreenshotAgeDays = Math.Clamp(value.ScreenshotAgeDays, 1, 3650);
        value.TileWidth = double.IsFinite(value.TileWidth) ? Math.Clamp(value.TileWidth, 160, 280) : 180;
        value.PinnedFolders = (value.PinnedFolders ?? []).Where(p => !string.IsNullOrWhiteSpace(p) && Path.IsPathFullyQualified(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return value;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this));
        File.Move(temporary, path, true);
    }
}
