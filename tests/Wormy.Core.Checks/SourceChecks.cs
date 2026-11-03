using Microsoft.Data.Sqlite;
using Wormy.Core;
using Wormy.Infrastructure;

internal static class SourceChecks
{
    public static async Task RunAsync()
    {
        const string github = "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://raw.githubusercontent.com/repo/a?token=secret\r\n";
        if (DownloadOrigins.Parse(github) != new DownloadOrigin("GitHub", "raw.githubusercontent.com")) throw new Exception("Known domain classification failed.");
        if (DownloadOrigins.Parse("[ZoneTransfer]\nHostUrl=https://cdn.example.net/a\nReferrerUrl=https://mail.google.com/mail/x")?.Source != "Gmail") throw new Exception("Referrer resolution failed.");
        if (DownloadOrigins.Parse("[ZoneTransfer]\nHostUrl=https://github.com.evil.example/a")?.Source == "GitHub") throw new Exception("Source suffix spoof matched.");
        if (DownloadOrigins.Parse("[ZoneTransfer]\nHostUrl=https://github.com@evil.example/a")?.Source == "GitHub") throw new Exception("URL userinfo spoof matched.");
        foreach (var invalid in new[] { "[ZoneTransfer]\nZoneId=3", "[Other]\nHostUrl=https://github.com/a", "[ZoneTransfer]\nHostUrl=file:///C:/data", "[ZoneTransfer]\nHostUrl=garbage", "[ZoneTransfer]\nHostUrl=https://github.com/a\nHostUrl=https://evil.example/a", new string('x', 16_385) })
            if (DownloadOrigins.Parse(invalid) is not null) throw new Exception("Malformed or oversized source accepted.");
        var now = DateTime.UtcNow;
        var sourced = new FileEntry(@"C:\data\note.txt", "note.txt", 1, now) { DownloadSource = "GitHub", SourceHost = "github.com" };
        if (!SearchParser.Parse("documents from github").Matches(sourced) || SearchParser.Parse("documents from gmail").Matches(sourced) || SearchParser.Parse("from github").Matches(sourced with { DownloadSource = null })) throw new Exception("Source search scope failed.");
        if (SearchParser.Parse("\"from github\"").Sources.Count != 0) throw new Exception("Quoted source phrase was parsed.");
        var root = Path.Combine(Path.GetTempPath(), "Wormy-source-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "note.txt"); await File.WriteAllTextAsync(path, "crimson telescope");
            var file = new FileEntry(path, "note.txt", new FileInfo(path).Length, File.GetLastWriteTimeUtc(path)) { DownloadSource = "GitHub", SourceHost = "github.com" };
            var database = Path.Combine(root, "index.db");
            using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
            {
                connection.Open(); using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE Roots(RootKey TEXT PRIMARY KEY,Path TEXT NOT NULL,ScannedTicks INTEGER NOT NULL); CREATE TABLE Files(RootKey TEXT NOT NULL REFERENCES Roots(RootKey) ON DELETE CASCADE,PathKey TEXT NOT NULL,Path TEXT NOT NULL,Name TEXT NOT NULL,Length INTEGER NOT NULL,ModifiedTicks INTEGER NOT NULL,PRIMARY KEY(RootKey,PathKey)); CREATE TABLE Content(PathKey TEXT PRIMARY KEY,Length INTEGER NOT NULL,ModifiedTicks INTEGER NOT NULL,Source TEXT NOT NULL); CREATE VIRTUAL TABLE ContentSearch USING fts5(PathKey UNINDEXED,Text,tokenize='unicode61'); PRAGMA user_version=2;"; command.ExecuteNonQuery();
            }
            var index = new LibraryIndex(database); await index.ReplaceRootAsync(root, [file], CancellationToken.None);
            await index.StoreContentAsync(file, "crimson telescope", "Text", CancellationToken.None);
            var restored = (await new LibraryIndex(database).LoadAsync()).Files.Single();
            if (restored.AddedUtc is null || restored != file with { AddedUtc = restored.AddedUtc } || (await index.LoadRootFilesAsync(root)).Single() != restored) throw new Exception("Source migration/restart persistence failed.");
            await index.ReplaceRootAsync(root, [file with { DownloadSource = "Gmail", SourceHost = "mail.google.com" }], CancellationToken.None);
            if ((await index.SearchContentAsync(["crimson"], CancellationToken.None)).Count != 1) throw new Exception("Source-only change lost indexed content.");
            if (OperatingSystem.IsWindows())
            {
                var data = Path.Combine(root, "data"); Directory.CreateDirectory(data); var live = Path.Combine(data, "live.txt"); await File.WriteAllTextAsync(live, "unchanged original");
                await File.WriteAllTextAsync(live + ":Zone.Identifier", github);
                var scan = await FolderScanner.ScanAsync(data, CancellationToken.None);
                if (scan.Files.Single().DownloadSource != "GitHub" || await File.ReadAllTextAsync(live) != "unchanged original") throw new Exception("Native ADS reader failed or changed original.");
                await index.ReplaceRootAsync(data, scan.Files, CancellationToken.None);
                await File.WriteAllTextAsync(live + ":Zone.Identifier", "[ZoneTransfer]\nHostUrl=https://canva.com/design/a");
                if (!await AutomaticRefresh.RefreshAsync(index, data, CancellationToken.None) || (await index.LoadRootFilesAsync(data)).Single().DownloadSource != "Canva") throw new Exception("Automatic source-only update failed.");
                await File.WriteAllTextAsync(live + ":Zone.Identifier", "[ZoneTransfer]\nZoneId=3");
                await AutomaticRefresh.RefreshAsync(index, data, CancellationToken.None);
                if ((await index.LoadRootFilesAsync(data)).Single().DownloadSource is not null) throw new Exception("Source removal left stale attribution.");
            }
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("Source checks passed: parsing, spoof boundaries, query scope, schema-2 migration, persistence, content preservation and native ADS refresh.");
    }

    public static async Task CreateFixtureAsync(string root)
    {
        if (Directory.Exists(root)) throw new IOException("Use a new fixture directory.");
        var data = Path.Combine(root, "files"); Directory.CreateDirectory(data);
        foreach (var (name, url) in new[] { ("GitHub-note.txt", "https://github.com/project/a"), ("Gmail-note.txt", "https://mail.google.com/mail/a"), ("Website-note.txt", "https://example.org/a"), ("Unknown-note.txt", "") })
        {
            var path = Path.Combine(data, name); await File.WriteAllTextAsync(path, "Generated crimson telescope note.");
            if (url.Length > 0) await File.WriteAllTextAsync(path + ":Zone.Identifier", "[ZoneTransfer]\nZoneId=3\nHostUrl=" + url);
        }
        var index = new LibraryIndex(Path.Combine(root, "library.db"));
        await index.ReplaceRootAsync(data, (await FolderScanner.ScanAsync(data, CancellationToken.None)).Files, CancellationToken.None);
        Console.WriteLine($"Source fixture: {Path.Combine(root, "library.db")}");
    }
}
