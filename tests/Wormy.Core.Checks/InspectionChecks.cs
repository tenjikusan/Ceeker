using Wormy.Core;

internal static class InspectionChecks
{
    public static async Task RunAsync()
    {
        var now = DateTime.UtcNow;
        var a = new FileEntry(@"C:\data\a.txt", "a.txt", 1, now);
        var b = a with { Path = @"C:\data\b.txt", Name = "b.txt" };
        var c = a with { Path = @"C:\data\c.txt", Name = "c.txt" };
        var displayed = new List<FileEntry> { c, a, b };
        var sequence = new PreviewSequence(displayed, a with { Path = a.Path.ToUpperInvariant() });
        displayed.Clear();
        if (sequence.Count != 3 || sequence.Position != 1 || sequence.Current != a || !sequence.CanPrevious || !sequence.CanNext) throw new Exception("Displayed snapshot/order or path casing failed.");
        if (!sequence.Move(1) || sequence.Current != b || sequence.CanNext || sequence.Move(1) || sequence.Current != b) throw new Exception("Next/end boundary failed.");
        if (!sequence.Move(-1) || !sequence.Move(-1) || sequence.Current != c || sequence.CanPrevious || sequence.Move(-1)) throw new Exception("Previous/start boundary failed.");
        if (sequence.Move(int.MaxValue) || sequence.Move(int.MinValue)) throw new Exception("Navigation overflow failed.");
        var fallback = new PreviewSequence([], a);
        if (fallback.Current != a || fallback.Count != 1 || fallback.CanNext || fallback.CanPrevious) throw new Exception("Startup/absent result fallback failed.");
        var root = Path.Combine(Path.GetTempPath(), "Wormy-inspection-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "details.txt"); await File.WriteAllTextAsync(path, "generated");
            var modified = now.AddDays(-3); File.SetLastWriteTimeUtc(path, modified);
            var inspection = FileInspection.Read(path);
            if (inspection.Length != 9 || inspection.ModifiedUtc != File.GetLastWriteTimeUtc(path) || inspection.CreatedUtc != File.GetCreationTimeUtc(path) || inspection.Attributes != File.GetAttributes(path) || await File.ReadAllTextAsync(path) != "generated") throw new Exception("Read-only live details failed.");
            await File.AppendAllTextAsync(path, " changed");
            if (FileInspection.Read(path).Length != 17) throw new Exception("Live details retained stale size.");
            try { FileInspection.Read(Path.Combine(root, "absent.txt")); throw new Exception("Missing details were accepted."); } catch (FileNotFoundException) { }
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("Inspection checks passed: frozen displayed order, casing, navigation boundaries/overflow/fallback, live metadata and missing-file reporting.");
    }
}
