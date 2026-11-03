using Microsoft.Win32;

namespace Wormy.App;
public static class AppUsage
{
    public static DateTime? LastRun(byte[] bytes, DateTime nowUtc)
    {
        if (bytes.Length != 72 || BitConverter.ToUInt32(bytes, 4) == 0) return null;
        try { var date = DateTime.FromFileTimeUtc(BitConverter.ToInt64(bytes, 60)); return date.Year >= 2000 && date <= nowUtc ? date : null; } catch (ArgumentOutOfRangeException) { return null; }
    }
    public static string DecodeName(string name) => new(name.Select(c => c is >= 'a' and <= 'z' ? (char)('a' + (c-'a'+13)%26) : c is >= 'A' and <= 'Z' ? (char)('A'+(c-'A'+13)%26) : c).ToArray());
    public static Dictionary<string, DateTime> Read(CancellationToken token)
    {
        var result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase); var count = 0;
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist", false);
            if (root is null) return result;
            foreach (var guid in root.GetSubKeyNames())
            {
                using var entries = root.OpenSubKey(guid + @"\Count", false); if (entries is null) continue;
                foreach (var name in entries.GetValueNames())
                {
                    token.ThrowIfCancellationRequested(); if (++count > 10000) return result;
                    var path = Environment.ExpandEnvironmentVariables(DecodeName(name));
                    if (path.StartsWith("UEME_RUNPATH:", StringComparison.OrdinalIgnoreCase)) path = path[13..];
                    if (!Path.IsPathFullyQualified(path) || entries.GetValue(name) is not byte[] bytes || LastRun(bytes, DateTime.UtcNow) is not { } date) continue;
                    if (!result.TryGetValue(path, out var previous) || date > previous) result[path] = date;
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return result;
    }
    public static DateTime? ForApp(IReadOnlyDictionary<string, DateTime> observations, string? location, string? icon)
    {
        var paths = new List<DateTime>();
        var executable = icon?.Split(',')[0].Trim().Trim('"');
        if (executable is not null && observations.TryGetValue(executable, out var exact)) paths.Add(exact);
        if (!string.IsNullOrWhiteSpace(location) && Path.IsPathFullyQualified(location))
            paths.AddRange(observations.Where(p => p.Key.StartsWith(Path.TrimEndingDirectorySeparator(location) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).Select(p=>p.Value));
        return paths.Count == 0 ? null : paths.Max();
    }
    public static bool IsUnused(InstalledApp app, DateTime nowUtc) => app.LastObservedLaunchUtc is { } date && date < nowUtc.AddDays(-90);
}
