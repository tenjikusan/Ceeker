using System.IO.Compression;
using System.Text;
using Wormy.Core;
using Wormy.Infrastructure;

internal static class PresentationChecks
{
    private static void Write(string path, string first = "launch &amp; forecast", string second = "blue comet", bool external = false)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Part(string name, string xml) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8); writer.Write(xml); }
        Part("ppt/presentation.xml", "<p:presentation xmlns:p='http://schemas.openxmlformats.org/presentationml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><p:sldIdLst><p:sldId r:id='second'/><p:sldId r:id='first'/></p:sldIdLst></p:presentation>");
        Part("ppt/_rels/presentation.xml.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='first' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide' Target='slides/slide1.xml'/><Relationship Id='second' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide' Target='slides/slide2.xml'" + (external ? " TargetMode='External'" : "") + "/></Relationships>");
        string Slide(string text) => "<p:sld xmlns:p='http://schemas.openxmlformats.org/presentationml/2006/main' xmlns:a='http://schemas.openxmlformats.org/drawingml/2006/main'><p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>" + text + "</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld></p:sld>";
        Part("ppt/slides/slide1.xml", Slide(first)); Part("ppt/slides/slide2.xml", Slide(second));
    }
    public static async Task CreateFixtureAsync(string directory)
    {
        if (Directory.Exists(directory)) throw new IOException("Use a new fixture directory.");
        var files = Path.Combine(directory, "files"); Directory.CreateDirectory(files);
        Write(Path.Combine(files, "launch.pptx")); Write(Path.Combine(files, "other.pptx"), "unrelated", "garden");
        var scan = await FolderScanner.ScanAsync(files, CancellationToken.None);
        await new LibraryIndex(Path.Combine(directory, "library.db")).ReplaceRootAsync(files, scan.Files, CancellationToken.None);
    }
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Wormy-pptx-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "slides.pptx"); Write(path);
            var original = await File.ReadAllBytesAsync(path);
            var result = await DocumentText.ReadAsync(path, CancellationToken.None);
            if (result.Text != "blue comet\nlaunch & forecast\n" || result.Truncated) throw new Exception("PPTX slide order/text extraction failed.");
            var scan = await FolderScanner.ScanAsync(root, CancellationToken.None);
            var indexPath = Path.Combine(root, "library.db"); var index = new LibraryIndex(indexPath);
            await index.ReplaceRootAsync(root, scan.Files, CancellationToken.None);
            await index.StoreContentAsync(scan.Files.Single(), result.Text, "PPTX", CancellationToken.None);
            if ((await new LibraryIndex(indexPath).SearchContentAsync(["blue comet"], CancellationToken.None)).Count != 1) throw new Exception("PPTX persisted phrase search failed.");
            var after = await File.ReadAllBytesAsync(path);
            if (!original.SequenceEqual(after)) throw new Exception("PPTX original changed.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await DocumentText.ReadAsync(path, cancelled.Token); throw new Exception("PPTX cancellation failed."); } catch (OperationCanceledException) { }
            var large = Path.Combine(root, "large.pptx"); Write(large, second: new string('x', 64_001));
            var limited = await DocumentText.ReadAsync(large, CancellationToken.None);
            if (!limited.Truncated || limited.Text.Length != 64_000) throw new Exception("PPTX text limit failed.");
            var external = Path.Combine(root, "external.pptx"); Write(external, external: true);
            try { await DocumentText.ReadAsync(external, CancellationToken.None); throw new Exception("PPTX external target accepted."); } catch (InvalidDataException) { }
            var blank = Path.Combine(root, "blank.pptx"); Write(blank, "", "");
            try { await DocumentText.ReadAsync(blank, CancellationToken.None); throw new Exception("Blank PPTX accepted."); } catch (NotSupportedException) { }
            var corrupt = Path.Combine(root, "corrupt.pptx"); await File.WriteAllTextAsync(corrupt, "invalid");
            try { await DocumentText.ReadAsync(corrupt, CancellationToken.None); throw new Exception("Corrupt PPTX accepted."); } catch (InvalidDataException) { }
            Console.WriteLine("PASS: PPTX ordered slide text, entities, persistent search, unchanged original, cancellation, text cap and blank/corrupt/external rejection.");
        }
        finally { Directory.Delete(root, true); }
    }
}
