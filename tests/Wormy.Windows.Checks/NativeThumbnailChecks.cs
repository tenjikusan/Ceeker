using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Wormy.App;
using Wormy.Core;
using Wormy.Infrastructure;

internal static class NativeThumbnailChecks
{
    public static async Task CreateFixtureAsync(string root)
    {
        if (Directory.Exists(root)) throw new IOException("Use a new generated fixture directory.");
        var files = Path.Combine(root, "files"); Directory.CreateDirectory(files);
        var image = Path.Combine(files, "Preview.png");
        using (var bitmap = new Bitmap(400, 200))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font("Arial", 30))
        { graphics.Clear(Color.FromArgb(235, 240, 255)); graphics.DrawString("WORMY PREVIEW", font, Brushes.DarkBlue, 15, 65); bitmap.Save(image, ImageFormat.Png); }
        var pdf=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        var fontId=pdf.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        pdf.AddPage(600,300).AddText("Generated Wormy preview",24,new UglyToad.PdfPig.Core.PdfPoint(30,200),fontId);
        await File.WriteAllBytesAsync(Path.Combine(files,"Report.pdf"),pdf.Build());
        foreach (var extension in new[] { "docx", "xlsx", "pptx" })
        {
            var path=Path.Combine(files,"EmbeddedPreview."+extension);
            using var archive=ZipFile.Open(path,ZipArchiveMode.Create);
            var contentTypes=archive.CreateEntry("[Content_Types].xml");
            using(var writer=new StreamWriter(contentTypes.Open())) writer.Write("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"png\" ContentType=\"image/png\"/></Types>");
            var preview = archive.CreateEntry("docProps/thumbnail.png"); using var destination = preview.Open(); using var original = File.OpenRead(image); original.CopyTo(destination);
        }
        var composition = new MediaComposition();
        composition.Clips.Add(await MediaClip.CreateFromImageFileAsync(await StorageFile.GetFileFromPathAsync(image), TimeSpan.FromSeconds(1)));
        var folder = await StorageFolder.GetFolderFromPathAsync(files);
        var video = await folder.CreateFileAsync("Preview.mp4");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var result = await composition.RenderToFileAsync(video, MediaTrimmingPreference.Precise, MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Wvga)).AsTask(timeout.Token);
        if (result != TranscodeFailureReason.None) throw new Exception($"Generated video failed: {result}");
        await File.WriteAllTextAsync(Path.Combine(files, "Unknown.wormyfixture"), "Generated unsupported thumbnail fixture");
        await File.WriteAllTextAsync(Path.Combine(files, "Broken.pdf"), "Generated corrupt PDF fixture");
        var index = new LibraryIndex(Path.Combine(root, "library.db"));
        await index.ReplaceRootAsync(files, (await FolderScanner.ScanAsync(files, CancellationToken.None)).Files, CancellationToken.None);
        await RunAsync(files);
        Console.WriteLine($"Generated thumbnail fixture: {root}");
    }
    public static async Task RunAsync(string files)
    {
        if(!Directory.Exists(files)) throw new IOException("Thumbnail fixture directory is missing.");
        var cache = new ThumbnailCache(NativeThumbnails.ReadAsync);
        var scan=await FolderScanner.ScanAsync(files,CancellationToken.None);
        if(scan.Errors.Count>0 || scan.Files.Count==0) throw new IOException("Thumbnail fixture scan was incomplete or empty.");
        foreach (var entry in scan.Files)
        {
            var before = await File.ReadAllBytesAsync(entry.Path);
            var data = await cache.GetAsync(entry, CancellationToken.None);
            if (entry.Extension is ".png" or ".pdf" or ".docx" or ".xlsx" or ".pptx" or ".mp4" && !entry.Name.StartsWith("Broken") && (data is null || data.IsIcon || data.Bytes.Length == 0)) throw new Exception($"Real preview missing: {entry.Name}");
            var after = await File.ReadAllBytesAsync(entry.Path);
            if (!before.SequenceEqual(after)) throw new Exception("Thumbnail changed original file.");
            Console.WriteLine($"Thumbnail {entry.Name}: {(data is null ? "category icon fallback" : data.IsIcon ? "Windows icon" : $"preview ({data.Bytes.Length} bytes)")}");
        }
        Console.WriteLine("PASS: Windows image/PDF/video/embedded Office previews, corrupt/unsupported icon fallbacks and original preservation.");
    }
}
