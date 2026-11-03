using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using Wormy.Core;
using Wormy.Infrastructure;

internal static class ContentChecks
{
    public static async Task CreateFixtureAsync(string directory)
    {
        if (Directory.Exists(directory)) throw new IOException("Use a new fixture directory.");
        var files = Path.Combine(directory, "files"); Directory.CreateDirectory(files);
        await File.WriteAllTextAsync(Path.Combine(files, "note.txt"), "The crimson telescope arrived on Tuesday. Invoice 12345.");
        await File.WriteAllTextAsync(Path.Combine(files, "other.txt"), "An unrelated garden note.");
        WriteDocx(Path.Combine(files, "report.docx"), "<w:p><w:r><w:t>crimson telescope</w:t></w:r></w:p><w:p><w:r><w:t>Project report</w:t></w:r></w:p>");
        var image = Path.GetFullPath("artifacts/ocr-fixture.png");
        if (File.Exists(image)) File.Copy(image, Path.Combine(files, "receipt.png"));
        var scan = await FolderScanner.ScanAsync(files, CancellationToken.None);
        await new LibraryIndex(Path.Combine(directory, "library.db")).ReplaceRootAsync(files, scan.Files, CancellationToken.None);
        Console.WriteLine($"Content UI fixture: {Path.GetFullPath(Path.Combine(directory, "library.db"))}");
    }

    private static void WriteDocx(string path, string body)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open(), Encoding.UTF8);
        writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" + body + "</w:body></w:document>");
    }

    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Wormy-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var data = Path.Combine(root, "data"); Directory.CreateDirectory(data);
            var path = Path.Combine(data, "note.txt"); await File.WriteAllTextAsync(path, "Crimson telescope invoice café.");
            var docx = Path.Combine(data, "report.docx");
            WriteDocx(docx, "<w:p><w:r><w:t>crimson </w:t></w:r><w:r><w:t>telescope</w:t></w:r></w:p><w:p><w:r><w:t>Second paragraph</w:t></w:r></w:p>");
            var extracted = await DocumentText.ReadAsync(docx, CancellationToken.None);
            if (extracted.Text != "crimson telescope\nSecond paragraph\n" || extracted.Truncated) throw new Exception("DOCX paragraph/run extraction failed.");
            var database = Path.Combine(root, "v1.db");
            using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
            {
                connection.Open(); using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE Roots(RootKey TEXT PRIMARY KEY,Path TEXT NOT NULL,ScannedTicks INTEGER NOT NULL); CREATE TABLE Files(RootKey TEXT NOT NULL REFERENCES Roots(RootKey) ON DELETE CASCADE,PathKey TEXT NOT NULL,Path TEXT NOT NULL,Name TEXT NOT NULL,Length INTEGER NOT NULL,ModifiedTicks INTEGER NOT NULL,PRIMARY KEY(RootKey,PathKey)); PRAGMA user_version=1;";
                command.ExecuteNonQuery();
            }
            var index = new LibraryIndex(database);
            var scan = await FolderScanner.ScanAsync(data, CancellationToken.None);
            await index.ReplaceRootAsync(data, scan.Files, CancellationToken.None);
            foreach (var file in scan.Files)
            {
                var result = await DocumentText.ReadAsync(file.Path, CancellationToken.None);
                await index.StoreContentAsync(file, result.Text, "Test", CancellationToken.None);
            }
            index = new LibraryIndex(database);
            if ((await index.SearchContentAsync(["CRIMSON telescope"], CancellationToken.None)).Count != 2) throw new Exception("Migrated index phrase/restart search failed.");
            if ((await index.SearchContentAsync(["invoice", "café"], CancellationToken.None)).Count != 1) throw new Exception("Unicode AND search failed.");
            if ((await index.SearchContentAsync(["tele"], CancellationToken.None)).Count != 0) throw new Exception("Whole word semantics failed.");
            if ((await index.SearchContentAsync(["\" OR crimson"], CancellationToken.None)).Count != 0) throw new Exception("FTS syntax was interpreted.");
            var note = scan.Files.Single(file => file.Path == path);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await index.StoreContentAsync(note, "replacement", "Test", cancelled.Token); throw new Exception("Cancelled write accepted."); } catch (OperationCanceledException) { }
            if ((await index.SearchContentAsync(["invoice"], CancellationToken.None)).Count != 1) throw new Exception("Cancelled write altered content.");
            await index.ReplaceRootAsync(root, scan.Files, CancellationToken.None);
            await index.RemoveRootAsync(root);
            if ((await index.SearchContentAsync(["invoice"], CancellationToken.None)).Count != 1) throw new Exception("Overlap disconnect lost content.");
            await index.ReplaceRootAsync(root, scan.Files, CancellationToken.None);
            await File.WriteAllTextAsync(path, "Changed document contents with a different length.");
            var updated = await FolderScanner.ScanAsync(data, CancellationToken.None);
            await index.ReplaceRootAsync(data, updated.Files, CancellationToken.None);
            if ((await index.SearchContentAsync(["invoice"], CancellationToken.None)).Count != 0) throw new Exception("Refresh retained stale content.");
            await index.RemoveRootAsync(root);
            try { await index.StoreContentAsync(note, "stale", "Test", CancellationToken.None); throw new Exception("Stale content write accepted."); } catch (IOException) { }
            await index.RemoveFilesAsync([docx]);
            if ((await index.SearchContentAsync(["crimson"], CancellationToken.None)).Count != 0) throw new Exception("Removed file content retained.");
            var large = Path.Combine(data, "large.txt"); await File.WriteAllTextAsync(large, new string('x', 64_001));
            if (!(await DocumentText.ReadAsync(large, CancellationToken.None)).Truncated) throw new Exception("Content truncation failed.");
            var corrupt = Path.Combine(data, "corrupt.docx"); await File.WriteAllTextAsync(corrupt, "not a ZIP");
            try { await DocumentText.ReadAsync(corrupt, CancellationToken.None); throw new Exception("Corrupt DOCX accepted."); } catch (InvalidDataException) { }
            await index.RemoveRootAsync(data);
            if ((await index.LoadAsync()).Files.Count != 0 || (await index.SearchContentAsync([], CancellationToken.None)).Count != 0) throw new Exception("Disconnect did not clear index.");
            if (!File.Exists(path)) throw new Exception("Indexing modified original file lifecycle.");
            Console.WriteLine("PASS: DOCX/text extraction, schema migration, persistent FTS phrase/Unicode/AND search, literal query escaping, cancellation, overlap retention, stale-content pruning and disconnect.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
