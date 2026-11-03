using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

namespace Wormy.App;

internal static class FileActions
{
    public static async Task<string> OpenAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        return await Launcher.LaunchFileAsync(file) ? "Opened in your default application" : "Windows could not open this file. Check its default application.";
    }
    public static async Task<string> OpenManyAsync(IEnumerable<string> paths)
    {
        var selected=paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (selected.Length is < 1 or > 10) return "Select 1–10 files to open in their default applications.";
        var opened=0; var failed=0;
        foreach(var path in selected)
        {
            try { var file=await StorageFile.GetFileFromPathAsync(path); if(await Launcher.LaunchFileAsync(file)) opened++; else failed++; }
            catch { failed++; }
        }
        return $"Opened {opened:N0} files · {failed:N0} could not be opened";
    }
    public static string Reveal(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("File is no longer available. Refresh the library.");
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        Process.Start(new ProcessStartInfo(explorer) { UseShellExecute = true, Arguments = $"/select,\"{path}\"" });
        return "Shown in Explorer";
    }
    public static string CopyPath(string path)
    {
        var data = new DataPackage(); data.SetText(path); Clipboard.SetContent(data);
        return "Path copied";
    }
    public static async Task<string> RevealManyAsync(IEnumerable<string> paths)
    {
        var selected = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (selected.Length == 0) return "Select files to show in Explorer";
        var groups = selected.GroupBy(Path.GetDirectoryName, StringComparer.OrdinalIgnoreCase).ToArray();
        if (selected.Length > 100 || groups.Length > 10)
            return "Select up to 100 files in up to 10 folders to show in Explorer.";
        var shown = 0;
        var unavailable = 0;
        foreach (var group in groups)
        {
            try
            {
                if (group.Key is null) { unavailable += group.Count(); continue; }
                var folder = await StorageFolder.GetFolderFromPathAsync(group.Key);
                var options = new FolderLauncherOptions();
                foreach (var path in group)
                {
                    try { options.ItemsToSelect.Add(await StorageFile.GetFileFromPathAsync(path)); }
                    catch { unavailable++; }
                }
                if (options.ItemsToSelect.Count == 0) continue;
                if (await Launcher.LaunchFolderAsync(folder, options)) shown += options.ItemsToSelect.Count;
                else unavailable += options.ItemsToSelect.Count;
            }
            catch { unavailable += group.Count(); }
        }
        return $"Shown in Explorer: {shown:N0} files" + (unavailable == 0 ? "" : $" · {unavailable:N0} unavailable or could not be shown");
    }
    public static void CopyText(string text)
    {
        var data = new DataPackage(); data.SetText(text); Clipboard.SetContent(data);
    }
    public static string CopyPaths(IEnumerable<string> paths)
    {
        var selected = paths.ToArray();
        if (selected.Length == 0) return "Select files to copy their paths";
        CopyText(string.Join(Environment.NewLine, selected));
        return $"{selected.Length:N0} paths copied";
    }
}
