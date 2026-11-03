using System.Text;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using Wormy.Core;

namespace Wormy.App;

public sealed record PdfContentResult(string Text, bool Truncated, int OcrPages, int SkippedOcrPages);

public static class PdfContent
{
    public const int MaximumOcrPages = 20;
    public static async Task<PdfContentResult> ReadAsync(string path, IProgress<string>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > DocumentText.MaximumBytes) throw new IOException("PDF missing or larger than 20 MB.");
        if ((info.Attributes & (FileAttributes.Offline | FileAttributes.ReparsePoint)) != 0) throw new IOException("Linked or offline PDFs are unsupported.");
        var bytes = await File.ReadAllBytesAsync(path, token);
        if (bytes.Length > DocumentText.MaximumBytes) throw new IOException("PDF grew beyond 20 MB.");
        using var parsed = await Task.Run(() => UglyToad.PdfPig.PdfDocument.Open(bytes, new UglyToad.PdfPig.ParsingOptions { UseLenientParsing = false, MaxStackDepth = 128 }), token);
        using var input = new InMemoryRandomAccessStream();
        PdfDocument? rendered = null;
        var text = new StringBuilder(); var ocrPages = 0; var skipped = 0;
        var pages = Math.Min(parsed.NumberOfPages, DocumentText.MaximumPdfPages);
        var truncated = pages < parsed.NumberOfPages;
        for (var number = 1; number <= pages; number++)
        {
            token.ThrowIfCancellationRequested();
            progress?.Report($"PDF page {number}/{pages} · reading text");
            var pageText = await Task.Run(() => UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(parsed.GetPage(number)), token);
            if (string.IsNullOrWhiteSpace(pageText))
            {
                if (ocrPages >= MaximumOcrPages) { skipped++; truncated = true; continue; }
                if (rendered is null)
                {
                    await input.WriteAsync(bytes.AsBuffer()).AsTask(token); input.Seek(0);
                    rendered = await PdfDocument.LoadFromStreamAsync(input).AsTask(token);
                    if (rendered.PageCount != parsed.NumberOfPages) throw new IOException("PDF parsers disagree about page count.");
                }
                progress?.Report($"PDF page {number}/{pages} · recognizing text locally");
                using var page = rendered.GetPage((uint)(number - 1));
                using var image = new InMemoryRandomAccessStream();
                var scale = Math.Min(300d / 72, 2400 / Math.Max(Math.Max(page.Size.Width, page.Size.Height), 1));
                await page.RenderToStreamAsync(image, new PdfPageRenderOptions
                {
                    DestinationWidth = (uint)Math.Max(1, page.Size.Width * scale),
                    DestinationHeight = (uint)Math.Max(1, page.Size.Height * scale),
                    BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255)
                }).AsTask(token);
                image.Seek(0);
                var imageBytes = new byte[checked((int)image.Size)];
                using (var reader = new DataReader(image.GetInputStreamAt(0)))
                { await reader.LoadAsync((uint)imageBytes.Length).AsTask(token); reader.ReadBytes(imageBytes); }
                pageText = (await ImageOcr.ReadPngAsync(imageBytes, token, highAccuracy: true)).Text;
                ocrPages++;
            }
            token.ThrowIfCancellationRequested();
            var remaining = TextPreview.MaximumCharacters - text.Length;
            if (pageText.Length >= remaining)
            { text.Append(pageText.AsSpan(0, remaining)); truncated |= pageText.Length > remaining || number < parsed.NumberOfPages; break; }
            text.Append(pageText); text.Append('\n');
        }
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text.ToString())) throw new NotSupportedException("No text detected in the inspected PDF pages. English OCR may not recognize this scan.");
        return new(text.ToString(), truncated, ocrPages, skipped);
    }
}
