using Windows.ApplicationModel.DataTransfer;
using Wormy.App;
using Wormy.Core;

internal static class TransferChecks
{
    public static async Task CreateFixtureAsync(string root)
    {
        if (Directory.Exists(root)) throw new IOException("Use a new generated fixture directory.");
        var dataRoot = Path.Combine(root, "files");
        var downloads = Path.Combine(dataRoot, "Downloads"); Directory.CreateDirectory(downloads);
        foreach (var name in new[] { "Invoice-A.png", "Invoice-B.png" })
        {
            var path = Path.Combine(downloads, name); File.Copy("artifacts/ocr-fixture.png", path);
            await File.WriteAllTextAsync(path + ":Zone.Identifier", "[ZoneTransfer]\nZoneId=3\nHostUrl=https://github.com/generated\n");
        }
        await File.WriteAllTextAsync(Path.Combine(downloads, "Broken.png"), "Generated invalid image");
        await File.WriteAllTextAsync(Path.Combine(dataRoot, "Outside.txt"), "Generated outside Downloads");
        var index = new Wormy.Infrastructure.LibraryIndex(Path.Combine(root, "library.db"));
        await index.ReplaceRootAsync(dataRoot, (await FolderScanner.ScanAsync(dataRoot, CancellationToken.None)).Files, CancellationToken.None);
        Console.WriteLine($"Generated batch fixture: {root}");
    }
    public static async Task RunAsync(string root)
    {
        var path = Path.Combine(root, "transfer.txt"); await File.WriteAllTextAsync(path, "Generated transfer fixture.");
        var info = new FileInfo(path);
        var file = new FileEntry(path, info.Name, info.Length, info.LastWriteTimeUtc);
        var selected = new List<FileEntry> { file };
        var error = "";
        void Validate(FileEntry entry)
        {
            var current = new FileInfo(entry.Path);
            if (!current.Exists || current.Length != entry.Length || current.LastWriteTimeUtc != entry.ModifiedUtc) throw new IOException("Changed file rejected");
        }
        var data = FileTransfer.Create(selected, Validate, message => error = message);
        selected.Clear();
        if (data.RequestedOperation != DataPackageOperation.Copy || !data.GetView().Contains(StandardDataFormats.StorageItems)) throw new Exception("Native copy format missing.");
        var items = await data.GetView().GetStorageItemsAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(25));
        if (items.Count != 1 || items[0].Path != path || error.Length != 0 || await File.ReadAllTextAsync(path) != "Generated transfer fixture.") throw new Exception("Transfer snapshot/original preservation failed.");
        var stale = FileTransfer.Create([file], Validate, message => error = message);
        await File.AppendAllTextAsync(path, "Changed");
        try { await stale.GetView().GetStorageItemsAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(25)); throw new Exception("Stale transfer accepted"); }
        catch (Exception) when (error == "Changed file rejected") { }
        Console.WriteLine("PASS: native StorageItems transfer, copy operation, frozen selection, unchanged originals and stale-file rejection.");
    }
}
