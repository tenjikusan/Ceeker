using Microsoft.Win32;
using Windows.Management.Deployment;

namespace Wormy.App;

public sealed record InstalledApp(string Id, string Name, string Version, string Publisher, string Scope, long? ReportedBytes, string? PackageFamily = null, DateTime? LastObservedLaunchUtc = null, string? ExecutablePath = null)
{
    public bool IsConnectedExecutable => Id.StartsWith("file:",StringComparison.Ordinal);
    public string? InstallLocation { get; init; }
}
public sealed record InstalledAppResult(IReadOnlyList<InstalledApp> Apps, int Unavailable, bool Limited);
public static class InstalledApps
{
    public static Uri SettingsUri(InstalledApp? app = null) => new(app?.PackageFamily is { Length: > 0 } family ? "ms-settings:appsfeatures-app?" + Uri.EscapeDataString(family) : "ms-settings:appsfeatures");
    public static long? EstimatedBytes(object? value)
    {
        long size = value switch { int i => (uint)i, long l => l, _ => -1 };
        return size > 0 && size <= long.MaxValue / 1024 ? size * 1024 : null;
    }
    public static IReadOnlyList<InstalledApp> Filter(IEnumerable<InstalledApp> apps, string query, bool largest) => apps.Where(a => a.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) || a.Publisher.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)).OrderBy(a => largest ? -(a.ReportedBytes ?? -1) : 0).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Id, StringComparer.Ordinal).ToArray();
    public static InstalledAppResult Read(CancellationToken token)
    {
        var apps = new List<InstalledApp>(); var unavailable = 0; var visited = 0; var limited = false;
        var usage = AppUsage.Read(token);
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in hive == RegistryHive.CurrentUser ? new[] { RegistryView.Registry64 } : new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var registry = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = registry.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", false);
                if (uninstall is null) continue;
                foreach (var name in uninstall.GetSubKeyNames())
                {
                    token.ThrowIfCancellationRequested(); if (++visited > 10000) { limited = true; break; }
                    try
                    {
                        using var key = uninstall.OpenSubKey(name, false); if (key is null) continue;
                        if (key.GetValue("SystemComponent") is int hidden && hidden == 1 || key.GetValue("ParentKeyName") is string) continue;
                        var display = key.GetValue("DisplayName") as string; if (string.IsNullOrWhiteSpace(display)) continue;
                        apps.Add(new($"{hive}/{view}/{name}", display.Trim(), key.GetValue("DisplayVersion") as string ?? "Version unavailable", key.GetValue("Publisher") as string ?? "Publisher unavailable", $"{(hive == RegistryHive.CurrentUser ? "Current user" : "All users")} · {(view == RegistryView.Registry64 ? "64-bit registration" : "32-bit registration")}", EstimatedBytes(key.GetValue("EstimatedSize")), LastObservedLaunchUtc: AppUsage.ForApp(usage, key.GetValue("InstallLocation") as string, key.GetValue("DisplayIcon") as string)) { InstallLocation = key.GetValue("InstallLocation") as string });
                    }
                    catch (System.Security.SecurityException) { unavailable++; }
                    catch (UnauthorizedAccessException) { unavailable++; }
                    catch (IOException) { unavailable++; }
                }
            }
            catch (System.Security.SecurityException) { unavailable++; }
            catch (UnauthorizedAccessException) { unavailable++; }
            catch (IOException) { unavailable++; }
        }
        try
        {
            var manager = new PackageManager();
            foreach (var package in manager.FindPackagesForUser(""))
            {
                token.ThrowIfCancellationRequested(); if (++visited > 10000) { limited = true; break; }
                try
                {
                    if (package.IsFramework || package.IsResourcePackage || package.IsBundle) continue;
                    var id = package.Id; var v = id.Version;
                    apps.Add(new(id.FullName, string.IsNullOrWhiteSpace(package.DisplayName) ? id.Name : package.DisplayName, $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}", package.PublisherDisplayName, "Current user · Packaged app", null, id.FamilyName) { InstallLocation=package.InstalledLocation?.Path });
                }
                catch { unavailable++; }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { unavailable++; }
        return new(Filter(apps.DistinctBy(a => a.Id, StringComparer.OrdinalIgnoreCase), "", false), unavailable, limited);
    }
}
