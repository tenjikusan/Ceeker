using System.Text;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
if (args.Length == 2 && args[0] == "--device-fixture") { await DeviceFixture.CreateAsync(args[1]); return; }
if (args.Length == 2 && args[0] == "--similarity-check") { await PhotoReviewChecks.RunAsync(Path.GetFullPath(args[1]),false); return; }
if (args.Length == 2 && args[0] == "--ocr-only") { var fixture = Path.GetFullPath(args[1]); Directory.CreateDirectory(fixture); await OcrChecks.RunAsync(fixture); Console.WriteLine("PASS: image OCR words, geometry and limits."); return; }
if (args.Length == 1 && args[0] == "--apps-check") { AppsChecks.Run(); return; }

if (args.Length == 2 && args[0] == "--pdf-ocr-fixture") { await PdfOcrChecks.CreateFixtureAsync(Path.GetFullPath(args[1])); return; }
if (args.Length == 2 && args[0] == "--pdf-ocr-check") { await PdfOcrChecks.RunAsync(Path.GetFullPath(args[1])); Console.WriteLine("PASS: PDF text/OCR extraction, limits, persistence and cancellation."); return; }
if (args.Length == 2 && args[0] == "--batch-fixture") { await TransferChecks.CreateFixtureAsync(Path.GetFullPath(args[1])); return; }
if (args.Length == 2 && args[0] == "--thumbnail-fixture") { await NativeThumbnailChecks.CreateFixtureAsync(Path.GetFullPath(args[1])); return; }
if (args.Length == 2 && args[0] == "--thumbnail-check") { await NativeThumbnailChecks.RunAsync(Path.GetFullPath(args[1])); return; }
if (args.Length == 2 && args[0] == "--media-check") { await MediaChecks.RunAsync(Path.GetFullPath(args[1])); return; }
if (args.Length == 2 && args[0] == "--photo-review-check") { await PhotoReviewChecks.RunAsync(Path.GetFullPath(args[1])); return; }
if (args.Length == 2 && args[0] == "--media-library")
{
    var folder = Path.GetFullPath(args[1]);
    var index = new Wormy.Infrastructure.LibraryIndex(folder + "-library.db");
    await index.ReplaceRootAsync(folder, (await Wormy.Core.FolderScanner.ScanAsync(folder, CancellationToken.None)).Files, CancellationToken.None);
    Console.WriteLine(folder + "-library.db"); return;
}

// A generated PDF; no personal documents are read.
var root = Path.Combine(Path.GetTempPath(), "Wormy-PDF-check-" + Guid.NewGuid());
Directory.CreateDirectory(root);
var recycleCheck = new TaskCompletionSource();
var recycleThread = new Thread(() =>
{
    try
    {
        var recyclePath = Path.Combine(root, "Wormy-generated-recycle-check-" + Guid.NewGuid() + ".txt");
        File.WriteAllText(recyclePath, "Disposable Wormy integration test fixture.");
        Wormy.App.RecycleBin.Move(recyclePath);
        if (File.Exists(recyclePath)) throw new Exception("Recycle operation did not move generated file.");
        Console.WriteLine("PASS: Windows shell recycled generated fixture on local NTFS. Fixture remains in Recycle Bin.");
        recycleCheck.SetResult();
    }
    catch (Exception ex) { recycleCheck.SetException(ex); }
});
recycleThread.SetApartmentState(ApartmentState.STA);
recycleThread.Start();
await recycleCheck.Task;
try
{
    await TransferChecks.RunAsync(root);
    await OcrChecks.RunAsync(root);
    await PdfOcrChecks.RunAsync(root);
    var cleanupRoot = Path.Combine(root, "cleanup");
    Directory.CreateDirectory(cleanupRoot);
    var keepPath = Path.Combine(cleanupRoot, "retained-generated.txt");
    var removePath = Path.Combine(cleanupRoot, "extra-generated.txt");
    await File.WriteAllTextAsync(keepPath, "Generated cleanup persistence fixture.");
    File.Copy(keepPath, removePath);
    var cleanupScan = await Wormy.Core.FolderScanner.ScanAsync(cleanupRoot, CancellationToken.None);
    var cleanupIndexPath = Path.Combine(root, "cleanup-library.db");
    var cleanupIndex = new Wormy.Infrastructure.LibraryIndex(cleanupIndexPath);
    await cleanupIndex.ReplaceRootAsync(cleanupRoot, cleanupScan.Files, CancellationToken.None);
    var choice = new Wormy.Core.CleanupChoice(cleanupScan.Files.Single(file => file.Path == keepPath), cleanupScan.Files.Single(file => file.Path == removePath));
    await Wormy.Core.DuplicateCleanup.RevalidateAndRemoveAsync(choice, path =>
    {
        var completion = new TaskCompletionSource();
        var worker = new Thread(() =>
        {
            try { Wormy.App.RecycleBin.Move(path); completion.SetResult(); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        completion.Task.GetAwaiter().GetResult();
    }, CancellationToken.None);
    await cleanupIndex.RemoveFilesAsync([removePath]);
    var restartedCleanup = await new Wormy.Infrastructure.LibraryIndex(cleanupIndexPath).LoadAsync();
    if (restartedCleanup.Files.Count != 1 || restartedCleanup.Files[0].Path != keepPath || File.Exists(removePath) || await File.ReadAllTextAsync(keepPath) != "Generated cleanup persistence fixture.")
        throw new Exception("Cleanup recycle/index/restart integration failed.");
    Console.WriteLine("PASS: revalidated generated duplicate recycled, retained contents preserved, metadata pruned, and restart restored retained record only.");
    var objects = new[]
    {
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 500] /Resources << /Font << /F1 5 0 R >> >> /Contents 6 0 R >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 500] /Resources << /Font << /F1 5 0 R >> >> /Contents 7 0 R >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        "BT /F1 24 Tf 40 400 Td (Wormy preview - page one) Tj ET",
        "BT /F1 24 Tf 40 400 Td (Wormy preview - page two) Tj ET"
    };
    var content = new StringBuilder("%PDF-1.4\n");
    var offsets = new List<int>();
    for (var i = 0; i < objects.Length; i++)
    {
        offsets.Add(content.Length);
        content.Append($"{i + 1} 0 obj\n");
        if (i >= 5) content.Append($"<< /Length {objects[i].Length} >>\nstream\n{objects[i]}\nendstream\n");
        else content.Append(objects[i] + "\n");
        content.Append("endobj\n");
    }
    var xref = content.Length;
    content.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
    foreach (var offset in offsets) content.Append($"{offset:D10} 00000 n \n");
    content.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    var path = Path.Combine(root, "preview.pdf");
    await File.WriteAllTextAsync(path, content.ToString(), Encoding.ASCII);
    var file = await StorageFile.GetFileFromPathAsync(path);
    var document = await PdfDocument.LoadFromFileAsync(file);
    if (document.PageCount != 2) throw new Exception("PDF page count failed.");
    for (uint i = 0; i < document.PageCount; i++)
    {
        using var page = document.GetPage(i);
        using var stream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = 400, DestinationHeight = 500 });
        if (stream.Size == 0) throw new Exception("PDF page rendering returned no data.");
    }
    var corruptPath = Path.Combine(root, "corrupt.pdf");
    await File.WriteAllTextAsync(corruptPath, "not a PDF");
    var rejected = false;
    try { await PdfDocument.LoadFromFileAsync(await StorageFile.GetFileFromPathAsync(corruptPath)); }
    catch { rejected = true; }
    if (!rejected) throw new Exception("Corrupt PDF accepted.");
    Console.WriteLine("PASS: Windows PDF load, two-page rendering, and corrupt-PDF rejection.");
}
finally { Directory.Delete(root, true); }
