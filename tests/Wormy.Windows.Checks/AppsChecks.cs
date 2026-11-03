using Wormy.App;
internal static class AppsChecks
{
    public static void Run()
    {
        var now = DateTime.UtcNow; var usage = new byte[72]; BitConverter.GetBytes((uint)2).CopyTo(usage,4); BitConverter.GetBytes(now.AddDays(-100).ToFileTimeUtc()).CopyTo(usage,60);
        if (AppUsage.LastRun(usage,now) != now.AddDays(-100) || AppUsage.LastRun(new byte[16],now) is not null || AppUsage.DecodeName("P:\\Grfg\\ncc.rkr") != "C:\\Test\\app.exe") throw new Exception("Launch observation parsing failed.");
        if (!AppUsage.IsUnused(new("a","App","1","Publisher","User",null,LastObservedLaunchUtc:now.AddDays(-100)),now) || AppUsage.IsUnused(new("b","Unknown","1","Publisher","User",null),now)) throw new Exception("Unknown use misclassified as unused.");
        if (InstalledApps.EstimatedBytes(2) != 2048 || InstalledApps.EstimatedBytes(0) is not null || InstalledApps.EstimatedBytes("2") is not null || InstalledApps.EstimatedBytes(long.MaxValue) is not null || InstalledApps.EstimatedBytes(-1) != uint.MaxValue * 1024L) throw new Exception("Reported size parsing failed.");
        InstalledApp[] records = [new("a", "Alpha", "1", "Publisher", "User", null), new("b", "Beta", "2", "Other", "Machine", 1024), new("c", "Gamma", "1", "Publisher", "Packaged", 2048, "Family_test")];
        if (InstalledApps.Filter(records, "publisher", false).Count != 2 || InstalledApps.Filter(records, "  BETA  ", false).Single().Id != "b" || InstalledApps.Filter(records, "", true).Select(a => a.Id).SequenceEqual(["c","b","a"]) == false) throw new Exception("App filtering/order failed.");
        if (InstalledApps.SettingsUri(records[0]).OriginalString != "ms-settings:appsfeatures" || InstalledApps.SettingsUri(records[2]).OriginalString != "ms-settings:appsfeatures-app?Family_test") throw new Exception("Windows management URI failed.");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { InstalledApps.Read(cancellation.Token); throw new Exception("App enumeration ignored cancellation."); } catch (OperationCanceledException) { }
        var actual = InstalledApps.Read(CancellationToken.None);
        if (actual.Apps.Count == 0 || actual.Apps.Any(a => string.IsNullOrWhiteSpace(a.Name)) || actual.Apps.Select(a => a.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != actual.Apps.Count) throw new Exception("Native app enumeration failed.");
        Console.WriteLine($"PASS: app filters/order, unknown/unsigned/overflow size metadata, settings URI, cancellation and read-only native enumeration. {actual.Apps.Count} registrations, {actual.Unavailable} unavailable, limited={actual.Limited}.");
    }
}
