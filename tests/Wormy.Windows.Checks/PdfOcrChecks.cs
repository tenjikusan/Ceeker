using System.Drawing;
using System.Drawing.Imaging;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using Wormy.App;
using Wormy.Core;
using Wormy.Infrastructure;

internal static class PdfOcrChecks
{
    private static byte[] ScanImage()
    {
        using var bitmap = new Bitmap(1200, 400); using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font("Arial", 48, FontStyle.Regular, GraphicsUnit.Pixel);
        graphics.Clear(Color.White); graphics.DrawString("SCANNED ARCHIVE", font, Brushes.Black, 50, 60);
        graphics.DrawString("Invoice 76543", font, Brushes.Black, 50, 170);
        using var stream = new MemoryStream(); bitmap.Save(stream, ImageFormat.Jpeg); return stream.ToArray();
    }

    private static void WritePdf(string path, int scannedPages, bool nativeText = false, bool blank = false)
    {
        var builder = new PdfDocumentBuilder();
        if (nativeText)
        {
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            builder.AddPage(600, 200).AddText("Selectable record 88897", 24, new PdfPoint(30, 130), font);
        }
        var jpeg = ScanImage();
        for (var i = 0; i < scannedPages; i++) builder.AddPage(600, 200).AddJpeg(jpeg, new PdfRectangle(0, 0, 600, 200));
        if (blank) builder.AddPage(600, 200);
        File.WriteAllBytes(path, builder.Build());
    }

    public static async Task CreateFixtureAsync(string root)
    {
        if (Directory.Exists(root)) throw new IOException("Use a new fixture directory.");
        var data = Path.Combine(root, "files"); Directory.CreateDirectory(data);
        WritePdf(Path.Combine(data, "scanned.pdf"), 1);
        WritePdf(Path.Combine(data, "hybrid.pdf"), 1, nativeText: true);
        WritePdf(Path.Combine(data, "blank.pdf"), 0, blank: true);
        var scan = await FolderScanner.ScanAsync(data, CancellationToken.None);
        await new LibraryIndex(Path.Combine(root, "library.db")).ReplaceRootAsync(data, scan.Files, CancellationToken.None);
        Console.WriteLine($"Scanned PDF UI fixture: {Path.GetFullPath(Path.Combine(root, "library.db"))}");
    }

    private sealed class InlineProgress(Action<string> action) : IProgress<string> { public void Report(string value) => action(value); }

    public static async Task RunAsync(string root)
    {
        var data = Path.Combine(root, "pdf-ocr"); Directory.CreateDirectory(data);
        var path = Path.Combine(data, "hybrid.pdf"); WritePdf(path, 1, nativeText: true);
        var original = File.ReadAllBytes(path);
        var result = await PdfContent.ReadAsync(path, null, CancellationToken.None);
        if (!result.Text.Contains("76543") || !result.Text.Contains("88897") || result.OcrPages != 1 || result.Truncated) throw new Exception($"Mixed PDF text/OCR failed: {result}");
        if (!original.SequenceEqual(File.ReadAllBytes(path))) throw new Exception("PDF OCR changed original bytes.");
        var fileInfo = new FileInfo(path); var file = new FileEntry(path, fileInfo.Name, fileInfo.Length, fileInfo.LastWriteTimeUtc);
        var database = Path.Combine(root, "pdf-ocr.db"); var index = new LibraryIndex(database);
        await index.ReplaceRootAsync(data, [file], CancellationToken.None);
        await index.StoreContentAsync(file, result.Text, "PDF OCR", CancellationToken.None);
        if (!(await new LibraryIndex(database).SearchContentAsync(["invoice 76543"], CancellationToken.None)).Contains(path)) throw new Exception("PDF OCR persistence/search failed.");
        var scanned = Path.Combine(data, "scanned.pdf"); WritePdf(scanned, 1);
        if (!(await PdfContent.ReadAsync(scanned, null, CancellationToken.None)).Text.Contains("76543")) throw new Exception("Image-only PDF OCR failed.");
        var blank = Path.Combine(data, "blank.pdf"); WritePdf(blank, 0, blank: true);
        try { await PdfContent.ReadAsync(blank, null, CancellationToken.None); throw new Exception("Blank PDF OCR accepted."); } catch (NotSupportedException) { }
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress(value => { if (value.StartsWith("PDF page 2/")) cancellation.Cancel(); });
        try { await PdfContent.ReadAsync(path, progress, cancellation.Token); throw new Exception("Between-page cancellation ignored."); } catch (OperationCanceledException) { }
        var capped = Path.Combine(data, "capped.pdf"); WritePdf(capped, 21);
        var limited = await PdfContent.ReadAsync(capped, null, CancellationToken.None);
        if (limited.OcrPages != 20 || limited.SkippedOcrPages != 1 || !limited.Truncated) throw new Exception("Scanned PDF OCR page cap failed.");
        var corrupt = Path.Combine(data, "corrupt.pdf"); File.WriteAllText(corrupt, "not a PDF");
        var rejected = false; try { await PdfContent.ReadAsync(corrupt, null, CancellationToken.None); } catch { rejected = true; }
        if (!rejected) throw new Exception("Corrupt PDF OCR accepted.");
        Console.WriteLine("PASS: image-only/mixed PDF OCR, original preservation, persisted search, blank/corrupt rejection, between-page cancellation and 20-page OCR cap.");
    }
}
