using System.Text;
using Wormy.Core;
using Wormy.Infrastructure;

internal static class PdfContentChecks
{
    public static void WritePdf(string path, params string[] pageStreams)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pageStreams.Length).Select(i => $"{4 + i * 2} 0 R"))}] /Count {pageStreams.Length} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        for (var i = 0; i < pageStreams.Length; i++)
        {
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 500] /Resources << /Font << /F1 3 0 R >> >> /Contents {5 + i * 2} 0 R >>");
            objects.Add($"<< /Length {pageStreams[i].Length} >>\nstream\n{pageStreams[i]}\nendstream");
        }
        var content = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++) { offsets.Add(content.Length); content.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = content.Length;
        content.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) content.Append($"{offset:D10} 00000 n \n");
        content.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllText(path, content.ToString(), Encoding.ASCII);
    }

    public static async Task CreateFixtureAsync(string directory)
    {
        if (Directory.Exists(directory)) throw new IOException("Use a new fixture directory.");
        var data = Path.Combine(directory, "files"); Directory.CreateDirectory(data);
        WritePdf(Path.Combine(data, "report.pdf"), "BT /F1 18 Tf 40 400 Td (Crimson telescope report) Tj ET", "BT /F1 18 Tf 40 400 Td (Invoice 54321 approved) Tj ET");
        WritePdf(Path.Combine(data, "blank-scan.pdf"), "q 0.5 g 40 40 100 100 re f Q");
        await File.WriteAllTextAsync(Path.Combine(data, "corrupt.pdf"), "not a PDF");
        var scan = await FolderScanner.ScanAsync(data, CancellationToken.None);
        await new LibraryIndex(Path.Combine(directory, "library.db")).ReplaceRootAsync(data, scan.Files, CancellationToken.None);
        Console.WriteLine($"PDF UI fixture: {Path.GetFullPath(Path.Combine(directory, "library.db"))}");
    }

    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Wormy-pdf-text-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "document.pdf");
            WritePdf(path, "BT /F1 18 Tf 40 400 Td (Crimson telescope report) Tj ET", "BT /F1 18 Tf 40 400 Td (Invoice 54321 approved) Tj ET");
            var result = await DocumentText.ReadAsync(path, CancellationToken.None);
            if (!result.Text.Contains("Crimson telescope report") || !result.Text.Contains("Invoice 54321 approved") || result.Truncated) throw new Exception("Multi-page PDF text failed.");
            var indexPath = Path.Combine(root, "library.db"); var index = new LibraryIndex(indexPath);
            var info = new FileInfo(path); var file = new FileEntry(path, info.Name, info.Length, info.LastWriteTimeUtc);
            await index.ReplaceRootAsync(root, [file], CancellationToken.None);
            await index.StoreContentAsync(file, result.Text, "PDF selectable text", CancellationToken.None);
            if (!(await new LibraryIndex(indexPath).SearchContentAsync(["invoice 54321"], CancellationToken.None)).Contains(path)) throw new Exception("PDF text FTS/restart failed.");
            var blank = Path.Combine(root, "blank.pdf"); WritePdf(blank, "q 0.5 g 40 40 100 100 re f Q");
            try { await DocumentText.ReadAsync(blank, CancellationToken.None); throw new Exception("No-text PDF accepted."); } catch (NotSupportedException ex) { if (!ex.Message.Contains("selectable text")) throw; }
            var corrupt = Path.Combine(root, "corrupt.pdf"); await File.WriteAllTextAsync(corrupt, "not a PDF");
            var rejected = false; try { await DocumentText.ReadAsync(corrupt, CancellationToken.None); } catch { rejected = true; }
            if (!rejected) throw new Exception("Corrupt PDF accepted.");
            var limit = Path.Combine(root, "page-limit.pdf");
            WritePdf(limit, Enumerable.Range(1, 201).Select(i => $"BT /F1 12 Tf 40 400 Td (Page marker {i}) Tj ET").ToArray());
            var limited = await DocumentText.ReadAsync(limit, CancellationToken.None);
            if (!limited.Truncated || !limited.Text.Contains("marker 200") || limited.Text.Contains("marker 201")) throw new Exception("PDF page bound failed.");
            var longPath = Path.Combine(root, "long.pdf"); WritePdf(longPath, "BT /F1 12 Tf 40 400 Td (" + new string('a', 65_000) + ") Tj ET");
            var longText = await DocumentText.ReadAsync(longPath, CancellationToken.None);
            if (!longText.Truncated || longText.Text.Length != TextPreview.MaximumCharacters) throw new Exception("PDF character bound failed.");
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            try { await DocumentText.ReadAsync(path, cancellation.Token); throw new Exception("Cancelled PDF extraction accepted."); } catch (OperationCanceledException) { }
            var large = Path.Combine(root, "large.pdf"); using (var stream = File.Create(large)) stream.SetLength(DocumentText.MaximumBytes + 1);
            try { await DocumentText.ReadAsync(large, CancellationToken.None); throw new Exception("Oversized PDF accepted."); } catch (IOException) { }
            var original = await File.ReadAllBytesAsync(path);
            await DocumentText.ReadAsync(path, CancellationToken.None);
            var afterExtraction = await File.ReadAllBytesAsync(path);
            if (!original.SequenceEqual(afterExtraction)) throw new Exception("PDF extraction changed original.");
            Console.WriteLine("PASS: multi-page PDF text, persistent content search, blank/corrupt rejection, page/character/file bounds, cancellation and original preservation.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
