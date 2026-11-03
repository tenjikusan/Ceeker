using System.IO.Compression;
using System.Text;
using Wormy.Core;
using Wormy.Infrastructure;

internal static class SpreadsheetChecks
{
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static void Write(string path, string cells, string shared = "<si><r><t>amber </t></r><r><t>orbit</t></r><rPh><t>excluded phonetic</t></rPh></si>", bool external = false, int sheetCount = 2)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Part(string name, string xml) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8); writer.Write(xml); }
        var sheets = string.Concat(Enumerable.Range(0, sheetCount).Select(i => $"<sheet name='Sheet{i}' sheetId='{i + 1}' r:id='s{i}' state='hidden'/>"));
        Part("xl/workbook.xml", $"<workbook xmlns='{Ns}' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets>{sheets}</sheets></workbook>");
        var relationships = string.Concat(Enumerable.Range(0, sheetCount).Select(i => $"<Relationship Id='s{i}' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet' Target='/xl/worksheets/s{i}.xml'" + (external ? " TargetMode='External'" : "") + "/>"));
        Part("xl/_rels/workbook.xml.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'>" + relationships + "<Relationship Id='strings' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings' Target='strings.xml'/></Relationships>");
        Part("xl/strings.xml", $"<sst xmlns='{Ns}'>{shared}</sst>");
        for (var i = 0; i < sheetCount; i++) Part($"xl/worksheets/s{i}.xml", $"<worksheet xmlns='{Ns}'><sheetData><row>" + (i == 0 ? cells : "<c t='inlineStr'><is><t>second worksheet</t></is></c>") + "</row></sheetData></worksheet>");
    }
    private const string Cells = "<c r='A1' t='s'><v>0</v></c><c r='B1' t='inlineStr'><is><t>Invoice &amp; budget</t></is></c><c r='C1'><v>76543</v></c><c r='D1'><f>SUM(C1)</f><v>99887</v></c><c r='E1'><f>NO_CACHE()</f></c><c r='F1' t='b'><v>1</v></c>";
    public static async Task CreateFixtureAsync(string directory)
    {
        if (Directory.Exists(directory)) throw new IOException("Use a new fixture directory.");
        var files = Path.Combine(directory, "files"); Directory.CreateDirectory(files);
        Write(Path.Combine(files, "budget.xlsx"), Cells);
        Write(Path.Combine(files, "other.xlsx"), "<c t='inlineStr'><is><t>garden</t></is></c>");
        var scan = await FolderScanner.ScanAsync(files, CancellationToken.None);
        await new LibraryIndex(Path.Combine(directory, "library.db")).ReplaceRootAsync(files, scan.Files, CancellationToken.None);
    }
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Wormy-xlsx-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "book.xlsx"); Write(path, Cells);
            var original = await File.ReadAllBytesAsync(path);
            var result = await DocumentText.ReadAsync(path, CancellationToken.None);
            if (result.Text != "amber orbit\tInvoice & budget\t76543\t99887\tTRUE\t\nsecond worksheet\t\n" || result.Truncated) throw new Exception("Excel cell types, sheet order or cached values failed.");
            var scan = await FolderScanner.ScanAsync(root, CancellationToken.None);
            var db = Path.Combine(root, "library.db"); var index = new LibraryIndex(db);
            await index.ReplaceRootAsync(root, scan.Files, CancellationToken.None);
            await index.StoreContentAsync(scan.Files.Single(), result.Text, "Excel", CancellationToken.None);
            var reopened = new LibraryIndex(db);
            if ((await reopened.SearchContentAsync(["amber orbit"], CancellationToken.None)).Count != 1 || (await reopened.SearchContentAsync(["99887"], CancellationToken.None)).Count != 1 || (await reopened.SearchContentAsync(["NO_CACHE"], CancellationToken.None)).Count != 0) throw new Exception("Excel persistence/formula search failed.");
            var after = await File.ReadAllBytesAsync(path); if (!original.SequenceEqual(after)) throw new Exception("Excel original changed.");
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            try { await DocumentText.ReadAsync(path, cancel.Token); throw new Exception("Excel cancellation failed."); } catch (OperationCanceledException) { }
            var cap = Path.Combine(root, "cap.xlsx"); Write(cap, "<c t='inlineStr'><is><t>" + new string('x', 64_001) + "</t></is></c>");
            var capped = await DocumentText.ReadAsync(cap, CancellationToken.None); if (!capped.Truncated || capped.Text.Length != 64_000) throw new Exception("Excel character limit failed.");
            var sheets = Path.Combine(root, "sheets.xlsx"); Write(sheets, Cells, sheetCount: 101); if (!(await DocumentText.ReadAsync(sheets, CancellationToken.None)).Truncated) throw new Exception("Excel sheet cap failed.");
            var huge = Path.Combine(root, "huge.xlsx"); Write(huge, Cells, "<si><t>" + new string('x', 8_000_001) + "</t></si>");
            try { await DocumentText.ReadAsync(huge, CancellationToken.None); throw new Exception("Excel XML cap failed."); } catch (IOException) { }
            var invalid = Path.Combine(root, "invalid.xlsx"); Write(invalid, "<c t='s'><v>999</v></c>");
            try { await DocumentText.ReadAsync(invalid, CancellationToken.None); throw new Exception("Invalid string accepted."); } catch (InvalidDataException) { }
            var external = Path.Combine(root, "external.xlsx"); Write(external, Cells, external: true);
            try { await DocumentText.ReadAsync(external, CancellationToken.None); throw new Exception("External Excel part accepted."); } catch (InvalidDataException) { }
            var blank = Path.Combine(root, "blank.xlsx"); Write(blank, "<c><f>UNCACHED()</f></c>", sheetCount: 1);
            try { await DocumentText.ReadAsync(blank, CancellationToken.None); throw new Exception("Blank workbook accepted."); } catch (NotSupportedException) { }
            var corrupt = Path.Combine(root, "corrupt.xlsx"); await File.WriteAllTextAsync(corrupt, "invalid");
            try { await DocumentText.ReadAsync(corrupt, CancellationToken.None); throw new Exception("Corrupt workbook accepted."); } catch (InvalidDataException) { }
            Console.WriteLine("PASS: Excel shared/rich/inline text, booleans, numeric/cached formula values, hidden sheets, persistence, original preservation, cancellation, sheet/XML/text caps and invalid/blank/external/corrupt rejection.");
        }
        finally { Directory.Delete(root, true); }
    }
}
