using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wormy.Core;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private int? dashboardDuplicateGroups;
    private readonly Queue<(DateTime Time, string Text)> dashboardEvents = new();
    private int dashboardDriveRequest;
    private async void UpdateDashboardVolumes()
    {
        var request=++dashboardDriveRequest;
        var paths=roots.Select(r=>Path.GetPathRoot(r.Path)).Where(p=>p is not null).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var summary=await Task.Run(() =>
        {
            decimal total=0,free=0; var count=0;
            foreach(var path in paths)
            {
                try { var drive=new DriveInfo(path); if(drive.DriveType==DriveType.Network || !drive.IsReady) continue; total+=drive.TotalSize; free+=drive.AvailableFreeSpace; count++; }
                catch { }
            }
            return total==0 ? "Volume capacity unavailable" : $"{count} local volume(s): {LibraryInsights.FormatBytes(total-free)} used of {LibraryInsights.FormatBytes(total)} · {free/total*100:F0}% available";
        });
        if(!closed && request==dashboardDriveRequest) DashboardDriveDetails.Text=summary;
    }
    private void RecordActivity(string text)
    {
        dashboardEvents.Enqueue((DateTime.Now,text));
        while (dashboardEvents.Count > 3) dashboardEvents.Dequeue();
        DashboardActivity.Text = string.Join("\n\n",dashboardEvents.Reverse().Select(e=>$"{e.Time:HH:mm}  {e.Text}"));
    }
    private void UpdateDashboard()
    {
        if (DashboardStorage is null) return;
        UpdateSidebarCounts();
        var total = files.Sum(f=>(decimal)Math.Max(0,f.Length));
        DashboardStorage.Text = LibraryInsights.FormatBytes(total);
        DashboardStorageBar.Children.Clear(); DashboardStorageBar.ColumnDefinitions.Clear();
        var categories = new[] { "Documents", "Images", "Videos", "Other" };
        var colors = new[] { Brush(255,90,31),Brush(255,170,133),Brush(123,130,137),Brush(201,206,211) };
        var details = new List<string>();
        for (var i=0;i<categories.Length;i++)
        {
            var name=categories[i];
            var bytes=files.Where(f=>name=="Other" ? !categories.Take(3).Contains(f.Category) : f.Category==name).Sum(f=>(decimal)Math.Max(0,f.Length));
            DashboardStorageBar.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(total==0 ? 1 : (double)bytes,GridUnitType.Star) });
            var segment=new Border { Background=colors[i] }; Grid.SetColumn(segment,i); DashboardStorageBar.Children.Add(segment);
            details.Add($"{name}  {LibraryInsights.FormatBytes(bytes)}  {(total==0 ? 0 : bytes/total*100):F0}%");
        }
        DashboardStorageDetails.Text=string.Join("\n",details);
        DashboardDuplicates.Content=dashboardDuplicateGroups?.ToString("N0") ?? "—";
        ToolTipService.SetToolTip(DashboardDuplicates,dashboardDuplicateGroups is null ? "Run comparison for duplicate counts" : "Open duplicate review");
        var now=DateTime.UtcNow;
        DashboardInstallers.Content=files.Count(f=>CleanupSuggestions.IsOldInstaller(f,now)).ToString("N0");
        DashboardVideos.Content=files.Count(f=>CleanupSuggestions.IsOldVideo(f,now,preferences.LargeFileMB*1_000_000L)).ToString("N0");
        var suggestions=CleanupSuggestions.Build(files,preferences.LargeFileMB*1_000_000L,preferences.DownloadAgeDays,preferences.ScreenshotAgeDays,now);
        DashboardCleanupBytes.Text=$"{LibraryInsights.FormatBytes(suggestions.Sum(s=>(decimal)Math.Max(0,s.File.Length)))} in file suggestions · review before recycling";
    }
    private void DashboardStorageClicked(object sender,RoutedEventArgs args)=>NavigateTo("Storage");
    private void DashboardDuplicatesClicked(object sender,RoutedEventArgs args)=>NavigateTo("Duplicates");
    private void DashboardInstallersClicked(object sender,RoutedEventArgs args)=>NavigateTo("Old installers",clearSearch:true,source:"All sources");
    private void DashboardVideosClicked(object sender,RoutedEventArgs args)=>NavigateTo("Old videos",clearSearch:true,source:"All sources");
}
