using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Wormy.Core;

public static class DocumentText
{
    public const long MaximumBytes = 20_000_000;
    public const int MaximumPdfPages = 200;
    public static bool Supports(string extension) => extension is ".txt" or ".md" or ".csv" or ".json" or ".log" or ".xml" or ".yaml" or ".yml" or ".ini" or ".cs" or ".docx" or ".pptx" or ".xlsx" or ".pdf";

    public static async Task<TextPreviewResult> ReadAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaximumBytes) throw new IOException("Document missing or larger than 20 MB.");
        if ((info.Attributes & (FileAttributes.Offline | FileAttributes.ReparsePoint)) != 0) throw new IOException("Linked or offline documents are unsupported.");
        if (info.Extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            return await Task.Run(() => ReadDocx(path, token), token);
        if (info.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return await Task.Run(() => ReadPdf(path, token), token);
        if (info.Extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase))
            return await Task.Run(() => PresentationText.Read(path, token), token);
        if (info.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return await Task.Run(() => SpreadsheetText.Read(path, token), token);
        if (!Supports(info.Extension.ToLowerInvariant())) throw new NotSupportedException("Unsupported document format.");
        return await TextPreview.ReadAsync(path, token);
    }

    private static TextPreviewResult ReadPdf(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var document = UglyToad.PdfPig.PdfDocument.Open(path, new UglyToad.PdfPig.ParsingOptions { UseLenientParsing = false, MaxStackDepth = 128 });
        var text = new StringBuilder();
        var pages = Math.Min(document.NumberOfPages, MaximumPdfPages);
        var truncated = document.NumberOfPages > pages;
        for (var pageNumber = 1; pageNumber <= pages; pageNumber++)
        {
            token.ThrowIfCancellationRequested();
            var page = document.GetPage(pageNumber);
            token.ThrowIfCancellationRequested();
            var pageText = UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(page);
            token.ThrowIfCancellationRequested();
            var remaining = TextPreview.MaximumCharacters - text.Length;
            if (pageText.Length > remaining)
            {
                text.Append(pageText.AsSpan(0, remaining)); truncated = true; break;
            }
            text.Append(pageText);
            if (text.Length == TextPreview.MaximumCharacters) { truncated |= pageNumber < document.NumberOfPages; break; }
            text.Append('\n');
        }
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text.ToString()))
            throw new NotSupportedException("No selectable text found in the first 200 PDF pages. Scanned-PDF OCR is not available yet.");
        return new(text.ToString(), truncated);
    }

    private static TextPreviewResult ReadDocx(string path, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("DOCX document body missing.");
        if (entry.Length > 8_000_000) throw new IOException("DOCX body exceeds the extraction limit.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8_000_000 });
        var text = new StringBuilder();
        var inText = false;
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            var word = reader.NamespaceURI == "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            if (reader.NodeType == XmlNodeType.Element && word)
            {
                if (reader.LocalName == "t") inText = !reader.IsEmptyElement;
                else if (reader.LocalName == "tab") text.Append('\t');
                else if (reader.LocalName == "br") text.Append('\n');
            }
            else if (reader.NodeType == XmlNodeType.EndElement && word)
            {
                if (reader.LocalName == "t") inText = false;
                else if (reader.LocalName == "p") text.Append('\n');
            }
            else if (inText && reader.NodeType is XmlNodeType.Text or XmlNodeType.SignificantWhitespace) text.Append(reader.Value);
            if (text.Length > TextPreview.MaximumCharacters)
                return new(text.ToString(0, TextPreview.MaximumCharacters), true);
        }
        return new(text.ToString(), false);
    }
}
