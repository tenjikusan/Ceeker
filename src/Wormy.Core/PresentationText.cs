using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Wormy.Core;

internal static class PresentationText
{
    private const int MaximumSlides = 200;
    private const long MaximumXmlBytes = 8_000_000;
    private static readonly XNamespace Presentation = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";

    public static TextPreviewResult Read(string path, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(path);
        long xmlBytes = 0;
        XDocument Load(string name)
        {
            token.ThrowIfCancellationRequested();
            var entry = archive.GetEntry(name) ?? throw new InvalidDataException("PowerPoint slide or presentation part missing.");
            xmlBytes = checked(xmlBytes + entry.Length);
            if (xmlBytes > MaximumXmlBytes) throw new IOException("PowerPoint XML exceeds the 8 MB extraction limit.");
            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumXmlBytes });
            var document = XDocument.Load(reader);
            token.ThrowIfCancellationRequested();
            return document;
        }

        var presentation = Load("ppt/presentation.xml");
        var relationships = Load("ppt/_rels/presentation.xml.rels").Root?.Elements(PackageRelationships + "Relationship")
            .ToDictionary(e => (string?)e.Attribute("Id") ?? throw new InvalidDataException("PowerPoint relationship ID missing."));
        if (presentation.Root?.Name != Presentation + "presentation" || relationships is null)
            throw new InvalidDataException("Invalid PowerPoint presentation.");
        var slides = presentation.Root.Element(Presentation + "sldIdLst")?.Elements(Presentation + "sldId").ToArray() ?? [];
        var text = new StringBuilder();
        var truncated = slides.Length > MaximumSlides;
        foreach (var slide in slides.Take(MaximumSlides))
        {
            token.ThrowIfCancellationRequested();
            var id = (string?)slide.Attribute(OfficeRelationships + "id");
            if (id is null || !relationships.TryGetValue(id, out var relationship) ||
                (string?)relationship.Attribute("Type") != OfficeRelationships.NamespaceName + "/slide" ||
                (string?)relationship.Attribute("TargetMode") == "External")
                throw new InvalidDataException("Invalid PowerPoint slide relationship.");
            var target = (string?)relationship.Attribute("Target") ?? throw new InvalidDataException("PowerPoint slide target missing.");
            var part = new Uri(new Uri("https://package.invalid/ppt/presentation.xml"), target);
            if (part.Host != "package.invalid" || part.Query.Length != 0 || part.Fragment.Length != 0)
                throw new InvalidDataException("External PowerPoint slide target unsupported.");
            var slideDocument = Load(Uri.UnescapeDataString(part.AbsolutePath.TrimStart('/')));
            if (slideDocument.Root?.Name != Presentation + "sld") throw new InvalidDataException("Invalid PowerPoint slide.");
            foreach (var paragraph in slideDocument.Descendants(Drawing + "p"))
            {
                foreach (var element in paragraph.Descendants())
                {
                    token.ThrowIfCancellationRequested();
                    var value = element.Name == Drawing + "t" ? element.Value : element.Name == Drawing + "br" ? "\n" : "";
                    var available = TextPreview.MaximumCharacters - text.Length;
                    if (value.Length > available) return new(text.Append(value.AsSpan(0, available)).ToString(), true);
                    text.Append(value);
                }
                if (text.Length == TextPreview.MaximumCharacters) return new(text.ToString(), true);
                text.Append('\n');
            }
        }
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text.ToString())) throw new NotSupportedException("No slide text found. PowerPoint image OCR is not supported.");
        return new(text.ToString(), truncated);
    }
}
