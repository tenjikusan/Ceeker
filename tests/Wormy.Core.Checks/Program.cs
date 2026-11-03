using Wormy.Core;
using Wormy.Infrastructure;
if(args.Length==3 && args[0]=="--saved-query")
{
    using var db=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource=Path.GetFullPath(args[1]),Mode=Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,Pooling=false }.ToString());
    db.Open(); using var command=db.CreateCommand(); command.CommandText="SELECT Path,Name,Length,ModifiedTicks,DownloadSource,SourceHost FROM Files;";
    using var rows=command.ExecuteReader(); var query=SearchParser.Parse(args[2]); var count=0;var matches=0;
    while(rows.Read()) { count++; var entry=new FileEntry(rows.GetString(0),rows.GetString(1),rows.GetInt64(2),new DateTime(rows.GetInt64(3),DateTimeKind.Utc)){DownloadSource=rows.IsDBNull(4)?null:rows.GetString(4),SourceHost=rows.IsDBNull(5)?null:rows.GetString(5)};if(FileSearch.Matches(query,entry,[],false))matches++; }
    Console.WriteLine($"READ-ONLY saved library: {count} rows; {matches} matches; {query.Terms.Count} residual terms. No data changed."); return;
}
if (args.Length == 3 && args[0] == "--read-only-scan")
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
    var scanRoot = Path.GetFullPath(args[1]);
    var result = await FolderScanner.ScanAsync(scanRoot, timeout.Token);
    var inventory = FolderInventory.Read(scanRoot, new ScanOptions(), timeout.Token);
    var queryCheck=SearchParser.Parse("images saved last week");
    Console.WriteLine($"Actual-drive query: {result.Files.Count(queryCheck.Matches):N0} global image/date matches; {result.Files.Count(file=>queryCheck.Matches(file)&&FileBrowsing.InView(file,"Cleanup review")):N0} within Cleanup review. Saved library unchanged.");
    var report = new { Root = scanRoot, FileCount = result.Files.Count, Errors = result.Errors, ChildFolderCount = inventory.Folders.Count, ChildFoldersUnavailable = inventory.Unavailable, ProtectedFilesIncluded = result.Files.Count(f => ProtectedFolders.IsVolumeMetadata(Path.GetDirectoryName(f.Path)!)), ProtectedFoldersShown = inventory.Folders.Count(f => ProtectedFolders.IsVolumeMetadata(f.Path)), SavedLibraryModified = false };
    await File.WriteAllTextAsync(Path.GetFullPath(args[2]), System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Read-only scan: {result.Files.Count:N0} files; {result.Errors.Count} scan issues; {inventory.Folders.Count} child folders; report saved.");
    Environment.ExitCode = result.Errors.Count == 0 && report.ProtectedFoldersShown == 0 && report.ProtectedFilesIncluded == 0 ? 0 : 1;
    return;
}
if (args.Length == 2 && args[0] == "--feature-fixture")
{
    var fixture=Path.GetFullPath(args[1]); if(Directory.Exists(fixture)) throw new IOException("Use a new fixture directory.");
    var folder=Path.Combine(fixture,"Summer Trip"); Directory.CreateDirectory(folder);
    File.Copy(Path.GetFullPath("artifacts/ocr-fixture.png"),Path.Combine(folder,"receipt.png"));
    var installer=Path.Combine(folder,"setup.exe"); await File.WriteAllTextAsync(installer,"Generated inert installer review fixture; not executable."); File.SetLastWriteTimeUtc(installer,DateTime.UtcNow.AddDays(-40));
    var video=Path.Combine(folder,"old.mp4"); using(var stream=File.Create(video)) stream.SetLength(2_000_000); File.SetLastWriteTimeUtc(video,DateTime.UtcNow.AddDays(-100));
    var index=new LibraryIndex(Path.Combine(fixture,"library.db")); await index.ReplaceRootAsync(folder,(await FolderScanner.ScanAsync(folder,CancellationToken.None)).Files,CancellationToken.None);
    await File.WriteAllTextAsync(Path.Combine(fixture,"library.db.view.json"),"{\"BackgroundOcr\":true,\"GettingStartedCompleted\":true,\"DarkTheme\":true,\"LargeFileMB\":1}");
    Console.WriteLine("Feature fixture: "+fixture); return;
}

UpdateChecks.Run();
LicenseChecks.Run();
CleanupPerformanceChecks.Run();
await ProtectedFolderChecks.RunAsync();
FeatureBatchChecks.Run();
await TagChecks.RunAsync();
if (args.Length == 1 && args[0] == "--updates-only") return;
SearchChecks.Run();
NaturalLanguageSearchChecks.Run();
await DateMetadataChecks.RunAsync();
await RelatedSearchChecks.RunAsync();
await FiveFeatureChecks.RunAsync();
BrowsingChecks.Run();
NavigationChecks.Run();
await DuplicateScopeChecks.RunAsync();
DuplicateRetentionChecks.Run();
await DesktopFeatureChecks.RunAsync();
InsightChecks.Run();
await InspectionChecks.RunAsync();
await ScanSettingsChecks.RunAsync();
await ThumbnailChecks.RunAsync();
await ReviewChecks.RunAsync();
if (args.Length == 2 && args[0] == "--scan-fixture") { await ScanSettingsChecks.CreateFixtureAsync(Path.GetFullPath(args[1])); return; }
if (args.Length == 1 && args[0] == "--inspection-only") return;
if (args.Length == 1 && args[0] == "--insights-only") return;
if (args.Length == 2 && args[0] == "--insights-fixture") { await InsightChecks.CreateFixtureAsync(Path.GetFullPath(args[1])); return; }
if (args.Length == 1 && args[0] == "--source-only") { await SourceChecks.RunAsync(); return; }
if (args.Length == 2 && args[0] == "--source-fixture") { await SourceChecks.RunAsync(); await SourceChecks.CreateFixtureAsync(Path.GetFullPath(args[1])); return; }
if (args.Length == 0) await SourceChecks.RunAsync();
if (args.Length == 1 && args[0] == "--browsing-only") return;
if (args.Length == 2 && args[0] == "--browsing-fixture")
{
    var fixture = Path.GetFullPath(args[1]);
    if (Directory.Exists(fixture)) throw new IOException("Use a new fixture directory.");
    var data = Path.Combine(fixture, "files");
    Directory.CreateDirectory(Path.Combine(data, "nested"));
    await File.WriteAllTextAsync(Path.Combine(data, "Alpha.txt"), "Generated alpha note.");
    await File.WriteAllTextAsync(Path.Combine(data, "nested", "Zulu.txt"), "Generated nested note.");
    await File.WriteAllTextAsync(Path.Combine(data, "Screenshot-note.txt"), "This is not an image.");
    await File.WriteAllBytesAsync(Path.Combine(data, "Screenshot-test.png"), Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="));
    using (var large = File.Create(Path.Combine(data, "Large.bin"))) large.SetLength(100_000_000);
    var fixtureIndex = new LibraryIndex(Path.Combine(fixture, "library.db"));
    await fixtureIndex.ReplaceRootAsync(data, (await FolderScanner.ScanAsync(data, CancellationToken.None)).Files, CancellationToken.None);
    var sibling = Path.Combine(fixture, "files-other"); Directory.CreateDirectory(sibling);
    await File.WriteAllTextAsync(Path.Combine(sibling, "Outside.txt"), "A different connected root.");
    await fixtureIndex.ReplaceRootAsync(sibling, (await FolderScanner.ScanAsync(sibling, CancellationToken.None)).Files, CancellationToken.None);
    Console.WriteLine($"Browsing fixture: {Path.Combine(fixture, "library.db")}"); return;
}
if (args.Length == 0) await AutomaticChecks.RunAsync();
if (args.Length == 2 && args[0] == "--xlsx-fixture") { await SpreadsheetChecks.CreateFixtureAsync(Path.GetFullPath(args[1])); return; }
await SpreadsheetChecks.RunAsync();
if (args.Length == 2 && args[0] == "--pptx-fixture") { await PresentationChecks.CreateFixtureAsync(Path.GetFullPath(args[1])); return; }
await PresentationChecks.RunAsync();
if (args.Length == 2 && args[0] == "--pdf-fixture") { await PdfContentChecks.CreateFixtureAsync(Path.GetFullPath(args[1])); return; }
if (args.Length == 2 && args[0] == "--content-fixture") { await ContentChecks.CreateFixtureAsync(Path.GetFullPath(args[1])); return; }
await ContentChecks.RunAsync();
await PdfContentChecks.RunAsync();
if (args.Length == 2 && args[0] == "--ui-fixture")
{
    var fixture = Path.GetFullPath(args[1]);
    if (Directory.Exists(fixture)) throw new IOException("Use a new fixture directory.");
    var data = Path.Combine(fixture, "files");
    Directory.CreateDirectory(data);
    await File.WriteAllTextAsync(Path.Combine(data, "A-original.txt"), "Generated Wormy fixture: alpha.");
    await File.WriteAllTextAsync(Path.Combine(data, "B-copy.txt"), "Generated Wormy fixture: alpha.");
    await File.WriteAllTextAsync(Path.Combine(data, "C-original.txt"), "Generated Wormy fixture: beta.");
    await File.WriteAllTextAsync(Path.Combine(data, "D-copy.txt"), "Generated Wormy fixture: beta.");
    await File.WriteAllTextAsync(Path.Combine(data, "Unique.txt"), "Generated unique note.");
    var fixtureScan = await FolderScanner.ScanAsync(data, CancellationToken.None);
    var fixtureIndex = new LibraryIndex(Path.Combine(fixture, "library.db"));
    await fixtureIndex.ReplaceRootAsync(data, fixtureScan.Files, CancellationToken.None);
    Console.WriteLine($"UI fixture index: {Path.Combine(fixture, "library.db")}");
    return;
}
if (args.Length == 2 && args[0] == "--performance") await PerformanceChecks.RunAsync(args[1]);

var root = Path.Combine(Path.GetTempPath(), "Wormy-check-" + Guid.NewGuid());
Directory.CreateDirectory(Path.Combine(root, "nested"));
try
{
    await File.WriteAllTextAsync(Path.Combine(root, "one.txt"), "hello");
    await File.WriteAllTextAsync(Path.Combine(root, "nested", "two.txt"), "world!");
    var textPreview = await TextPreview.ReadAsync(Path.Combine(root, "one.txt"), CancellationToken.None);
    if (textPreview.Text != "hello" || textPreview.Truncated) throw new Exception("Text preview failed.");
    var result = await FolderScanner.ScanAsync(root, CancellationToken.None);
    if (result.Files.Count != 2 || result.Files.Sum(file => file.Length) != 11 || result.Errors.Count != 0)
        throw new Exception("Recursive enumeration or logical file sizes failed.");
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try
    {
        await FolderScanner.ScanAsync(root, cancellation.Token);
        throw new Exception("Cancelled scan unexpectedly completed.");
    }
    catch (OperationCanceledException) { }
    var missing = await FolderScanner.ScanAsync(Path.Combine(root, "missing"), CancellationToken.None);
    if (missing.Errors.Count != 1) throw new Exception("Missing root did not produce a visible error.");
    var indexPath = Path.Combine(root, "state", "library.db");
    var index = new LibraryIndex(indexPath);
    await index.ReplaceRootAsync(root, result.Files, CancellationToken.None);
    var restored = await new LibraryIndex(indexPath).LoadAsync();
    if (restored.Roots.Count != 1 || restored.Files.Count != 2 || restored.Files.Sum(file => file.Length) != 11)
        throw new Exception("Index restart persistence failed.");
    try
    {
        await index.ReplaceRootAsync(root, [result.Files[0] with { Length = -1 }], CancellationToken.None);
        throw new Exception("Invalid snapshot unexpectedly committed.");
    }
    catch (Microsoft.Data.Sqlite.SqliteException) { }
    if ((await index.LoadAsync()).Files.Count != 2) throw new Exception("Failed transaction destroyed old snapshot.");
    try { await index.ReplaceRootAsync(root, [], cancellation.Token); throw new Exception("Cancelled write committed."); }
    catch (OperationCanceledException) { }
    if ((await index.LoadAsync()).Files.Count != 2) throw new Exception("Cancelled write destroyed old snapshot.");
    var nestedRoot = Path.Combine(root, "nested");
    var nestedFile = result.Files.Single(file => file.Path.StartsWith(nestedRoot));
    await index.ReplaceRootAsync(nestedRoot, [nestedFile], CancellationToken.None);
    if ((await index.LoadAsync()).Files.Count != 2) throw new Exception("Overlapping roots double-counted files.");
    await index.ReplaceRootAsync(root.ToUpperInvariant(), [result.Files[0]], CancellationToken.None);
    if ((await index.LoadAsync()).Roots.Count != 2) throw new Exception("Root casing created a duplicate root.");
    await index.RemoveRootAsync(root);
    var remaining = await index.LoadAsync();
    if (remaining.Roots.Count != 1 || remaining.Files.Count != 1 || remaining.Files[0].Path != nestedFile.Path)
        throw new Exception("Root removal lost another root's records.");
    if (!File.Exists(nestedFile.Path)) throw new Exception("Index operation modified an original file.");
    await index.ReplaceRootAsync(nestedRoot, [], CancellationToken.None);
    if ((await index.LoadAsync()).Files.Count != 0) throw new Exception("Refresh did not remove stale records.");
    var longText = Path.Combine(root, "long.txt");
    await File.WriteAllTextAsync(longText, new string('a', TextPreview.MaximumCharacters + 10));
    var bounded = await TextPreview.ReadAsync(longText, CancellationToken.None);
    if (!bounded.Truncated || bounded.Text.Length != TextPreview.MaximumCharacters) throw new Exception("Text preview limit failed.");
    await File.WriteAllTextAsync(longText, "Unicode: café తెలుగు", System.Text.Encoding.Unicode);
    if (!(await TextPreview.ReadAsync(longText, CancellationToken.None)).Text.Contains("తెలుగు")) throw new Exception("UTF-16 BOM decoding failed.");
    await File.WriteAllBytesAsync(longText, [65, 0, 66]);
    try { await TextPreview.ReadAsync(longText, CancellationToken.None); throw new Exception("Binary preview accepted."); }
    catch (InvalidDataException) { }
    try { await TextPreview.ReadAsync(Path.Combine(root, "one.txt"), cancellation.Token); throw new Exception("Cancelled preview completed."); }
    catch (OperationCanceledException) { }
    var duplicateRoot = Path.Combine(root, "duplicates");
    Directory.CreateDirectory(duplicateRoot);
    await File.WriteAllTextAsync(Path.Combine(duplicateRoot, "a.txt"), "same");
    await File.WriteAllTextAsync(Path.Combine(duplicateRoot, "b.txt"), "same");
    await File.WriteAllTextAsync(Path.Combine(duplicateRoot, "c.txt"), "diff");
    var duplicateFiles = (await FolderScanner.ScanAsync(duplicateRoot, CancellationToken.None)).Files;
    var duplicateProgress = new DuplicateProgressCapture();
    var duplicates = await DuplicateScanner.ScanAsync(duplicateFiles.Concat(duplicateFiles), duplicateProgress, CancellationToken.None);
    if (duplicateProgress.Found.Count != 1 || duplicateProgress.Found[0].Files.Count != 2 || duplicateProgress.Completed != 3) throw new Exception("Live duplicate group/progress delivery failed.");
    if (duplicates.Groups.Count != 1 || duplicates.Groups[0].Files.Count != 2 || duplicates.Skipped.Count != 0)
        throw new Exception("Exact duplicate grouping or path deduplication failed.");
    await File.WriteAllTextAsync(Path.Combine(duplicateRoot, "b.txt"), "changed");
    duplicates = await DuplicateScanner.ScanAsync(duplicateFiles, null, CancellationToken.None);
    if (duplicates.Groups.Count != 0 || duplicates.Skipped.Count != 1) throw new Exception("Stale duplicate metadata accepted.");
    try { await DuplicateScanner.ScanAsync(duplicateFiles, null, cancellation.Token); throw new Exception("Cancelled duplicate scan completed."); }
    catch (OperationCanceledException) { }
    if ((await DuplicateScanner.ScanAsync([], null, CancellationToken.None)).Groups.Count != 0) throw new Exception("Empty duplicate scan failed.");
    Console.WriteLine("PASS: exact hashes, same-size different content, path deduplication, stale-file rejection, empty library, and duplicate cancellation.");
    await File.WriteAllTextAsync(Path.Combine(duplicateRoot, "b.txt"), "same");
    duplicateFiles = (await FolderScanner.ScanAsync(duplicateRoot, CancellationToken.None)).Files;
    var keptFile = duplicateFiles.Single(file => file.Name == "a.txt");
    var removedFile = duplicateFiles.Single(file => file.Name == "b.txt");
    var calls = 0;
    await DuplicateCleanup.RevalidateAndRemoveAsync(new(keptFile, removedFile), path =>
    {
        if (path != removedFile.Path) throw new Exception("Wrong cleanup target.");
        try { using var write = new FileStream(keptFile.Path, FileMode.Open, FileAccess.Write); throw new Exception("Retained copy was not locked."); }
        catch (IOException) { }
        calls++;
    }, CancellationToken.None);
    await File.WriteAllTextAsync(removedFile.Path, "diff");
    File.SetLastWriteTimeUtc(removedFile.Path, removedFile.ModifiedUtc);
    try { await DuplicateCleanup.RevalidateAndRemoveAsync(new(keptFile, removedFile), _ => calls++, CancellationToken.None); throw new Exception("Changed content accepted."); }
    catch (IOException) { }
    try { await DuplicateCleanup.RevalidateAndRemoveAsync(new(keptFile, keptFile), _ => calls++, CancellationToken.None); throw new Exception("Keeper removal accepted."); }
    catch (IOException) { }
    try { await DuplicateCleanup.RevalidateAndRemoveAsync(new(keptFile, removedFile), _ => calls++, cancellation.Token); throw new Exception("Cancelled cleanup accepted."); }
    catch (OperationCanceledException) { }
    if (calls != 1 || !File.Exists(keptFile.Path) || !File.Exists(removedFile.Path)) throw new Exception("Cleanup safety gate failed.");
    await index.ReplaceRootAsync(duplicateRoot, duplicateFiles, CancellationToken.None);
    await index.RemoveFilesAsync([removedFile.Path.ToUpperInvariant()]);
    if ((await index.LoadAsync()).Files.Any(file => file.Path == removedFile.Path) || !File.Exists(removedFile.Path)) throw new Exception("Cleanup metadata pruning failed.");
    Console.WriteLine("PASS: cleanup content revalidation, keeper locking, same-path rejection, cancellation, and metadata pruning.");
    Console.WriteLine("PASS: bounded text preview, truncation, Unicode/BOM decoding, binary rejection, and cancellation.");
    Console.WriteLine("PASS: recursive scan, logical lengths, cancellation, and inaccessible-root reporting.");
    Console.WriteLine("PASS: SQLite restart persistence, rollback, cancellation, overlapping roots, root casing, disconnect safety, and stale-record removal.");
}
finally
{
    // This unique directory contains only generated test data.
    Directory.Delete(root, recursive: true);
}

