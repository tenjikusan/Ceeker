using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Wormy.Core;

internal static class SpreadsheetText
{
    private const int MaximumSheets = 100;
    private const long MaximumXmlBytes = 8_000_000;
    private static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Package = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static TextPreviewResult Read(string path, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(path);
        long xmlBytes = 0;
        XDocument Load(string name)
        {
            token.ThrowIfCancellationRequested();
            var entry = archive.GetEntry(name) ?? throw new InvalidDataException("Excel workbook part missing.");
            xmlBytes = checked(xmlBytes + entry.Length);
            if (xmlBytes > MaximumXmlBytes) throw new IOException("Excel XML exceeds the 8 MB extraction limit.");
            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumXmlBytes });
            var document = XDocument.Load(reader);
            token.ThrowIfCancellationRequested();
            return document;
        }
        var workbook = Load("xl/workbook.xml");
        if (workbook.Root?.Name != Sheet + "workbook") throw new InvalidDataException("Invalid Excel workbook.");
        var relationships = Load("xl/_rels/workbook.xml.rels").Root;
        if (relationships?.Name != Package + "Relationships") throw new InvalidDataException("Invalid Excel relationships.");
        var rels = relationships.Elements(Package + "Relationship").ToDictionary(r => (string?)r.Attribute("Id") ?? throw new InvalidDataException("Excel relationship ID missing."));
        string Resolve(XElement relationship, string type)
        {
            if ((string?)relationship.Attribute("Type") != Office.NamespaceName + "/" + type ||
                string.Equals((string?)relationship.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("External or unsupported Excel part.");
            var target = (string?)relationship.Attribute("Target") ?? throw new InvalidDataException("Excel part target missing.");
            if ((!target.StartsWith('/') && Uri.TryCreate(target, UriKind.Absolute, out _)) || target.StartsWith("//")) throw new InvalidDataException("External Excel target unsupported.");
            var uri = new Uri(new Uri("https://package.invalid/xl/workbook.xml"), target);
            if (uri.Host != "package.invalid" || uri.Query.Length != 0 || uri.Fragment.Length != 0) throw new InvalidDataException("Invalid Excel part target.");
            return Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        }
        string RichText(XElement element) => string.Concat(element.Elements().SelectMany(e => e.Name == Sheet + "t" ? new[] { e } : e.Name == Sheet + "r" ? e.Elements(Sheet + "t") : []).Select(e => e.Value));
        var shared = Array.Empty<string>();
        var sharedRelationship = rels.Values.SingleOrDefault(r => (string?)r.Attribute("Type") == Office.NamespaceName + "/sharedStrings");
        if (sharedRelationship is not null)
        {
            var strings = Load(Resolve(sharedRelationship, "sharedStrings"));
            if (strings.Root?.Name != Sheet + "sst") throw new InvalidDataException("Invalid Excel shared strings.");
            shared = strings.Root.Elements(Sheet + "si").Select(e => { token.ThrowIfCancellationRequested(); return RichText(e); }).ToArray();
        }
        var sheets = workbook.Root.Element(Sheet + "sheets")?.Elements(Sheet + "sheet").ToArray() ?? [];
        var text = new StringBuilder();
        bool Append(string value)
        {
            var available = TextPreview.MaximumCharacters - text.Length;
            text.Append(value.AsSpan(0, Math.Min(available, value.Length)));
            return value.Length <= available;
        }
        foreach (var sheet in sheets.Take(MaximumSheets))
        {
            token.ThrowIfCancellationRequested();
            var id = (string?)sheet.Attribute(Office + "id");
            if (id is null || !rels.TryGetValue(id, out var relationship)) throw new InvalidDataException("Excel sheet relationship missing.");
            // Chart sheets do not contain cells; their chart text is outside this extractor's scope.
            if ((string?)relationship.Attribute("Type") == Office.NamespaceName + "/chartsheet") continue;
            var worksheet = Load(Resolve(relationship, "worksheet"));
            if (worksheet.Root?.Name != Sheet + "worksheet") throw new InvalidDataException("Invalid Excel worksheet.");
            foreach (var row in worksheet.Root.Element(Sheet + "sheetData")?.Elements(Sheet + "row") ?? [])
            {
                foreach (var cell in row.Elements(Sheet + "c"))
                {
                    token.ThrowIfCancellationRequested();
                    var value = cell.Element(Sheet + "v")?.Value ?? "";
                    switch ((string?)cell.Attribute("t"))
                    {
                        case "s":
                            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index >= shared.Length) throw new InvalidDataException("Invalid Excel shared string reference.");
                            value = shared[index]; break;
                        case "inlineStr": value = cell.Element(Sheet + "is") is { } inline ? RichText(inline) : ""; break;
                        case "b": value = value switch { "1" => "TRUE", "0" => "FALSE", "" => "", _ => throw new InvalidDataException("Invalid Excel boolean.") }; break;
                    }
                    // Formula text is never evaluated; only the existing cached value is indexed.
                    if (value.Length == 0) continue;
                    if (!Append(value) || !Append("\t")) return new(text.ToString(), true);
                }
                if (!Append("\n")) return new(text.ToString(), true);
            }
        }
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text.ToString())) throw new NotSupportedException("No saved cell values found. Formulas are not calculated during indexing.");
        return new(text.ToString(), sheets.Length > MaximumSheets);
    }
}
