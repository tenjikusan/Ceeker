using Microsoft.Data.Sqlite;
using Wormy.Core;

namespace Wormy.Infrastructure;

public sealed record IndexedRoot(string Path, DateTime ScannedUtc)
{
    public string Name => System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(Path)) is { Length: > 0 } name ? name : Path;
}
public sealed record LibrarySnapshot(IReadOnlyList<IndexedRoot> Roots, IReadOnlyList<FileEntry> Files);

/// Stores metadata and explicitly extracted content. Original files are never modified.
public sealed class LibraryIndex(string databasePath)
{
    private static string Key(string path) => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path)).ToUpperInvariant();

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(databasePath))!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString());
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            var version = Convert.ToInt64(command.ExecuteScalar());
            if (version > 6) throw new InvalidOperationException("This index was created by a newer Ceeker version.");
            command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL;";
            command.ExecuteNonQuery();
            if (version == 0)
            {
                using var transaction = connection.BeginTransaction();
                command.Transaction = transaction;
                command.CommandText = """
                    CREATE TABLE IF NOT EXISTS Roots (RootKey TEXT PRIMARY KEY, Path TEXT NOT NULL, ScannedTicks INTEGER NOT NULL);
                    CREATE TABLE IF NOT EXISTS Files (RootKey TEXT NOT NULL REFERENCES Roots(RootKey) ON DELETE CASCADE,
                        PathKey TEXT NOT NULL, Path TEXT NOT NULL, Name TEXT NOT NULL,
                        Length INTEGER NOT NULL CHECK(Length>=0), ModifiedTicks INTEGER NOT NULL,
                        PRIMARY KEY(RootKey,PathKey));
                    CREATE INDEX IF NOT EXISTS IX_Files_Modified ON Files(ModifiedTicks);
                    PRAGMA user_version=1;
                    """;
                command.ExecuteNonQuery();
                transaction.Commit();
            }
            if (version < 2)
            {
                using var migration = connection.BeginTransaction();
                command.Transaction = migration;
                command.CommandText = """
                    CREATE TABLE Content (PathKey TEXT PRIMARY KEY, Length INTEGER NOT NULL, ModifiedTicks INTEGER NOT NULL, Source TEXT NOT NULL);
                    CREATE VIRTUAL TABLE ContentSearch USING fts5(PathKey UNINDEXED, Text, tokenize='unicode61');
                    PRAGMA user_version=2;
                    """;
                command.ExecuteNonQuery();
                migration.Commit();
            }
            if (version < 3)
            {
                using var migration = connection.BeginTransaction(); command.Transaction = migration;
                command.CommandText = "ALTER TABLE Files ADD COLUMN DownloadSource TEXT; ALTER TABLE Files ADD COLUMN SourceHost TEXT; PRAGMA user_version=3;";
                command.ExecuteNonQuery(); migration.Commit();
            }
            if (version < 4)
            {
                using var migration = connection.BeginTransaction(); command.Transaction = migration;
                command.CommandText = "CREATE TABLE Tags (PathKey TEXT NOT NULL, Tag TEXT NOT NULL COLLATE NOCASE, PRIMARY KEY(PathKey,Tag)); PRAGMA user_version=4;";
                command.ExecuteNonQuery(); migration.Commit();
            }
            if (version < 5)
            {
                using var migration = connection.BeginTransaction(); command.Transaction = migration;
                command.CommandText = "ALTER TABLE Files ADD COLUMN CreatedTicks INTEGER; ALTER TABLE Files ADD COLUMN AccessedTicks INTEGER; ALTER TABLE Files ADD COLUMN AddedTicks INTEGER; PRAGMA user_version=5;";
                command.ExecuteNonQuery(); migration.Commit();
            }
            if(version<6)
            {
                using var migration=connection.BeginTransaction(); command.Transaction=migration;
                command.CommandText="ALTER TABLE Files ADD COLUMN CaptureMetadata INTEGER NOT NULL DEFAULT 0; PRAGMA user_version=6;";
                command.ExecuteNonQuery(); migration.Commit();
            }
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    public Task<LibrarySnapshot> LoadAsync() => Task.Run(() =>
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Path, ScannedTicks FROM Roots ORDER BY Path;";
        var roots = new List<IndexedRoot>();
        using (var reader = command.ExecuteReader())
            while (reader.Read()) roots.Add(new(reader.GetString(0), new DateTime(reader.GetInt64(1), DateTimeKind.Utc)));
        command.CommandText = "SELECT Path,Name,Length,ModifiedTicks,DownloadSource,SourceHost,CreatedTicks,AccessedTicks,AddedTicks,CaptureMetadata FROM Files ORDER BY ModifiedTicks DESC;";
        var files = new Dictionary<string, FileEntry>(StringComparer.Ordinal);
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                var file = new FileEntry(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), new DateTime(reader.GetInt64(3), DateTimeKind.Utc)) { DownloadSource = reader.IsDBNull(4) ? null : reader.GetString(4), SourceHost = reader.IsDBNull(5) ? null : reader.GetString(5), CreatedUtc = reader.IsDBNull(6) ? null : new DateTime(reader.GetInt64(6),DateTimeKind.Utc), AccessedUtc = reader.IsDBNull(7) ? null : new DateTime(reader.GetInt64(7),DateTimeKind.Utc), AddedUtc = reader.IsDBNull(8) ? null : new DateTime(reader.GetInt64(8),DateTimeKind.Utc), CaptureMetadata=reader.GetInt64(9)!=0 };
                files.TryAdd(Key(file.Path), file);
            }
        transaction.Commit();
        return new LibrarySnapshot(roots, files.Values.ToArray());
    });

    public Task<IReadOnlyList<FileEntry>> LoadRootFilesAsync(string root) => Task.Run<IReadOnlyList<FileEntry>>(() =>
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT Path,Name,Length,ModifiedTicks,DownloadSource,SourceHost,CreatedTicks,AccessedTicks,AddedTicks,CaptureMetadata FROM Files WHERE RootKey=$root;";
        command.Parameters.AddWithValue("$root", Key(root));
        var result = new List<FileEntry>(); using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), new DateTime(reader.GetInt64(3), DateTimeKind.Utc)) { DownloadSource = reader.IsDBNull(4) ? null : reader.GetString(4), SourceHost = reader.IsDBNull(5) ? null : reader.GetString(5), CreatedUtc = reader.IsDBNull(6) ? null : new DateTime(reader.GetInt64(6),DateTimeKind.Utc), AccessedUtc = reader.IsDBNull(7) ? null : new DateTime(reader.GetInt64(7),DateTimeKind.Utc), AddedUtc = reader.IsDBNull(8) ? null : new DateTime(reader.GetInt64(8),DateTimeKind.Utc), CaptureMetadata=reader.GetInt64(9)!=0 });
        return result;
    });

    public Task ReplaceRootAsync(string root, IReadOnlyList<FileEntry> files, CancellationToken cancellationToken) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var rootKey = Key(root);
        var firstSeen = new Dictionary<string,long?>(StringComparer.Ordinal);
        command.CommandText = "SELECT PathKey,MIN(AddedTicks) FROM Files GROUP BY PathKey;";
        using (var reader = command.ExecuteReader()) while (reader.Read()) firstSeen[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetInt64(1);
        command.CommandText = "INSERT INTO Roots VALUES($key,$path,$ticks) ON CONFLICT(RootKey) DO UPDATE SET Path=$path,ScannedTicks=$ticks; DELETE FROM Files WHERE RootKey=$key;";
        command.Parameters.AddWithValue("$key", rootKey);
        command.Parameters.AddWithValue("$path", System.IO.Path.GetFullPath(root));
        command.Parameters.AddWithValue("$ticks", DateTime.UtcNow.Ticks);
        command.ExecuteNonQuery();
        command.Parameters.Clear();
        command.CommandText = "INSERT OR REPLACE INTO Files (RootKey,PathKey,Path,Name,Length,ModifiedTicks,DownloadSource,SourceHost,CreatedTicks,AccessedTicks,AddedTicks,CaptureMetadata) VALUES($root,$key,$path,$name,$length,$modified,$source,$host,$created,$accessed,$added,$capture);";
        command.Parameters.AddWithValue("$root", rootKey);
        var key = command.Parameters.Add("$key", SqliteType.Text);
        var path = command.Parameters.Add("$path", SqliteType.Text);
        var name = command.Parameters.Add("$name", SqliteType.Text);
        var length = command.Parameters.Add("$length", SqliteType.Integer);
        var modified = command.Parameters.Add("$modified", SqliteType.Integer);
        var source = command.Parameters.Add("$source", SqliteType.Text);
        var host = command.Parameters.Add("$host", SqliteType.Text);
        var created = command.Parameters.Add("$created",SqliteType.Integer);
        var accessed = command.Parameters.Add("$accessed",SqliteType.Integer);
        var added = command.Parameters.Add("$added",SqliteType.Integer);
        var capture=command.Parameters.Add("$capture",SqliteType.Integer);
        command.Prepare();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileKey = Key(file.Path);
            if (!fileKey.StartsWith(rootKey.EndsWith(System.IO.Path.DirectorySeparatorChar) ? rootKey : rootKey + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("Scan result is outside the selected root.");
            key.Value = fileKey; path.Value = file.Path; name.Value = file.Name;
            source.Value = (object?)file.DownloadSource ?? DBNull.Value; host.Value = (object?)file.SourceHost ?? DBNull.Value;
            length.Value = file.Length; modified.Value = file.ModifiedUtc.ToUniversalTime().Ticks;
            created.Value = (object?)file.CreatedUtc?.ToUniversalTime().Ticks ?? DBNull.Value;
            capture.Value=file.CaptureMetadata ? 1 : 0;
            accessed.Value = (object?)file.AccessedUtc?.ToUniversalTime().Ticks ?? DBNull.Value;
            added.Value = firstSeen.TryGetValue(fileKey,out var observed) ? (object?)observed ?? DBNull.Value : DateTime.UtcNow.Ticks;
            command.ExecuteNonQuery();
        }
        cancellationToken.ThrowIfCancellationRequested();
        command.Parameters.Clear();
        command.Parameters.AddWithValue("$root", rootKey);
        command.Parameters.AddWithValue("$prefix", rootKey.EndsWith(System.IO.Path.DirectorySeparatorChar) ? rootKey : rootKey + System.IO.Path.DirectorySeparatorChar);
        command.CommandText = """
            DELETE FROM Content WHERE substr(PathKey,1,length($prefix))=$prefix AND NOT EXISTS
                (SELECT 1 FROM Files f WHERE f.RootKey=$root AND f.PathKey=Content.PathKey AND f.Length=Content.Length AND f.ModifiedTicks=Content.ModifiedTicks);
            """;
        command.ExecuteNonQuery();
        PruneContent(connection, transaction);
        transaction.Commit();
    }, cancellationToken);

    public Task RemoveFilesAsync(IEnumerable<string> paths) => Task.Run(() =>
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM Files WHERE PathKey=$key;";
        var parameter = command.Parameters.Add("$key", SqliteType.Text);
        foreach (var path in paths) { parameter.Value = Key(path); command.ExecuteNonQuery(); }
        PruneContent(connection, transaction);
        transaction.Commit();
    });

    public Task RemoveRootAsync(string root) => Task.Run(() =>
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM Roots WHERE RootKey=$key;";
        command.Parameters.AddWithValue("$key", Key(root));
        command.ExecuteNonQuery();
        PruneContent(connection, transaction);
        transaction.Commit();
    });

    private static void PruneContent(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM Content WHERE NOT EXISTS (SELECT 1 FROM Files f WHERE f.PathKey=Content.PathKey AND f.Length=Content.Length AND f.ModifiedTicks=Content.ModifiedTicks);
            DELETE FROM ContentSearch WHERE PathKey NOT IN (SELECT PathKey FROM Content);
            DELETE FROM Tags WHERE PathKey NOT IN (SELECT PathKey FROM Files);
            """;
        command.ExecuteNonQuery();
    }

    public Task StoreContentAsync(FileEntry file, string text, string source, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        if (text.Length > TextPreview.MaximumCharacters) throw new ArgumentException("Extracted text exceeds the limit.");
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$key", Key(file.Path));
        command.Parameters.AddWithValue("$length", file.Length);
        command.Parameters.AddWithValue("$modified", file.ModifiedUtc.ToUniversalTime().Ticks);
        command.CommandText = "SELECT COUNT(*) FROM Files WHERE PathKey=$key AND Length=$length AND ModifiedTicks=$modified;";
        if (Convert.ToInt64(command.ExecuteScalar()) == 0) throw new IOException("File metadata changed or the folder is disconnected. Refresh first.");
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$source", source);
        command.CommandText = """
            INSERT INTO Content VALUES($key,$length,$modified,$source) ON CONFLICT(PathKey) DO UPDATE SET Length=$length,ModifiedTicks=$modified,Source=$source;
            DELETE FROM ContentSearch WHERE PathKey=$key;
            INSERT INTO ContentSearch(PathKey,Text) VALUES($key,$text);
            """;
        command.ExecuteNonQuery();
        token.ThrowIfCancellationRequested();
        transaction.Commit();
    }, token);

    public Task<Dictionary<string, string[]>> LoadTagsAsync(CancellationToken token = default) => Task.Run(() =>
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT f.Path,t.Tag FROM Tags t JOIN Files f ON f.PathKey=t.PathKey ORDER BY t.Tag;";
        var tags = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read()) { token.ThrowIfCancellationRequested(); var path = reader.GetString(0); if (!tags.TryGetValue(path, out var values)) tags[path] = values = []; values.Add(reader.GetString(1)); }
        return tags.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }, token);

    public Task SetTagsAsync(string path, IEnumerable<string> tags, CancellationToken token = default)
    {
        var values = tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (values.Length > 20 || values.Any(t => t.Length > 64 || t.Any(char.IsControl))) throw new ArgumentException("Use at most 20 tags, each up to 64 characters, without control characters.");
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested(); using var connection = Open(); using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.Parameters.AddWithValue("$key", Key(path));
            command.CommandText = "SELECT COUNT(*) FROM Files WHERE PathKey=$key;";
            if (Convert.ToInt64(command.ExecuteScalar()) == 0) throw new IOException("This file is no longer connected. Refresh first.");
            command.CommandText = "DELETE FROM Tags WHERE PathKey=$key;"; command.ExecuteNonQuery();
            command.Parameters.AddWithValue("$tag", ""); command.CommandText = "INSERT INTO Tags(PathKey,Tag) VALUES($key,$tag);";
            foreach (var tag in values) { token.ThrowIfCancellationRequested(); command.Parameters["$tag"].Value = tag; command.ExecuteNonQuery(); }
            token.ThrowIfCancellationRequested(); transaction.Commit();
        }, token);
    }

    public Task<Dictionary<string,string>> SemanticTextsAsync(CancellationToken token) => Task.Run(() =>
    {
        using var connection=Open(); using var command=connection.CreateCommand();
        command.CommandText="SELECT DISTINCT f.Path,substr(s.Text,1,2000) FROM Files f JOIN Content c ON c.PathKey=f.PathKey AND c.Length=f.Length AND c.ModifiedTicks=f.ModifiedTicks JOIN ContentSearch s ON s.PathKey=c.PathKey LIMIT 1000;";
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        using var reader=command.ExecuteReader(); while(reader.Read()) { token.ThrowIfCancellationRequested(); result[reader.GetString(0)]=reader.GetString(1); }
        return result;
    },token);
    public Task<HashSet<string>> SearchContentAsync(IReadOnlyList<string> terms, CancellationToken token, bool relatedWords = false) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT f.Path FROM ContentSearch s JOIN Content c ON c.PathKey=s.PathKey
            JOIN Files f ON f.PathKey=c.PathKey AND f.Length=c.Length AND f.ModifiedTicks=c.ModifiedTicks
            """;
        if (terms.Count > 0)
        {
            command.CommandText += " WHERE ContentSearch MATCH $query";
            command.Parameters.AddWithValue("$query", string.Join(" AND ", terms.Select(term => "(" + string.Join(" OR ",(relatedWords ? RelatedSearch.Alternatives(term) : new[] { term }).Select(candidate => "\""+candidate.Replace("\"","\"\"")+"\"")) + ")")));
        }
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read()) { token.ThrowIfCancellationRequested(); paths.Add(reader.GetString(0)); }
        return paths;
    }, token);
}
