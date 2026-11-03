using Wormy.Core;
using Wormy.Infrastructure;

internal static class TagChecks
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Wormy-tags-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "invoice.txt"); await File.WriteAllTextAsync(path, "original");
            var file = new FileEntry(path, "invoice.txt", 8, DateTime.UtcNow);
            var index = new LibraryIndex(Path.Combine(root, "library.db"));
            await index.ReplaceRootAsync(root, [file], CancellationToken.None);
            await index.SetTagsAsync(path, [" Work ", "work", "客户"]);
            var tags = (await new LibraryIndex(Path.Combine(root, "library.db")).LoadTagsAsync())[path];
            if (tags.Length != 2 || !tags.Contains("客户") || await File.ReadAllTextAsync(path) != "original") throw new Exception("Tag persistence/normalization changed originals.");
            if (!FileSearch.Matches(SearchParser.Parse("invoice work"), file, tags, false) || FileSearch.Matches(SearchParser.Parse("invoice missing"), file, tags, false)) throw new Exception("Combined name/tag terms failed.");
            if (!FileSearch.Matches(SearchParser.Parse("\"secret phrase\""), file, [], true) || FileSearch.Matches(SearchParser.Parse("pdfs \"secret phrase\""), file, [], true)) throw new Exception("Content match bypassed metadata filter.");
            try { await index.SetTagsAsync(path, [new string('x', 65)]); throw new Exception("Oversized tag accepted."); } catch (ArgumentException) { }
            if ((await index.LoadTagsAsync())[path].Length != 2) throw new Exception("Invalid edit replaced tags.");
            await index.ReplaceRootAsync(root, [file with { Length = 9 }], CancellationToken.None);
            if ((await index.LoadTagsAsync())[path].Length != 2) throw new Exception("Refresh lost tags.");
            await index.SetTagsAsync(path, []);
            if ((await index.LoadTagsAsync()).Count != 0) throw new Exception("Clear tags failed.");
            await index.SetTagsAsync(path, ["retained"]);
            await index.RemoveRootAsync(root);
            if ((await index.LoadTagsAsync()).Count != 0) throw new Exception("Disconnect left tags.");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("Tag checks passed: persistence, Unicode, normalization, original preservation, combined matching, filters, bounds, refresh, clear and disconnect.");
    }
}
