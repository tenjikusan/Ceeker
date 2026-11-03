using System.Drawing;
using Wormy.App;

if(args.Length==2&&args[0]=="--ui-fixture")
{
    var fixture=Path.GetFullPath(args[1]); if(Directory.Exists(fixture)) throw new IOException("Use a new fixture directory.");
    var data=Path.Combine(fixture,"Downloads"); Directory.CreateDirectory(data); Directory.CreateDirectory(Path.Combine(data,"Empty"));
    var text=Path.Combine(data,"record.txt"); await File.WriteAllTextAsync(text,"A bill requesting payment for purchased goods. Amount due: 1200.");
    var photo=Path.Combine(data,"test-image.png"); using(var bitmap=new Bitmap(256,256)) { using(var graphics=Graphics.FromImage(bitmap)) graphics.Clear(Color.Red); bitmap.Save(photo,System.Drawing.Imaging.ImageFormat.Png); }
    var index=new Wormy.Infrastructure.LibraryIndex(Path.Combine(fixture,"library.db")); var files=(await Wormy.Core.FolderScanner.ScanAsync(data,CancellationToken.None)).Files;
    await index.ReplaceRootAsync(data,files,CancellationToken.None); await index.StoreContentAsync(files.Single(f=>f.Name=="record.txt"),File.ReadAllText(text),"fixture",CancellationToken.None);
    await File.WriteAllTextAsync(Path.Combine(fixture,"library.db.view.json"),"{\"GettingStartedCompleted\":true,\"SemanticSearch\":true,\"DarkTheme\":true}"); Console.WriteLine("Generated isolated UI fixture: "+fixture); return;
}

InstallerChecks.Run();
if(args.Length==1 && args[0]=="--installer-only") return;
using var model=new LocalEmbeddings();
var query=model.Encode("an unpaid invoice for a purchase"); var related=model.Encode("a bill requesting payment for goods"); var unrelated=model.Encode("mountain climbing and a blue ocean sunset");
if(query.Length!=384||LocalEmbeddings.Similarity(query,related)<=LocalEmbeddings.Similarity(query,unrelated)+.1) throw new Exception("Local text semantic ranking failed.");
Console.WriteLine("PASS: real MiniLM inference, normalized 384-dimensional vectors and billing semantic ranking.");
var root=Path.Combine(Path.GetTempPath(),"Wormy-ai-"+Guid.NewGuid()); Directory.CreateDirectory(root);
try
{
    var path=Path.Combine(root,"red.png"); using(var bitmap=new Bitmap(256,256)) { using(var graphics=Graphics.FromImage(bitmap)) graphics.Clear(Color.Red); bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png); }
    using var vision=new LocalVision(); var image=await vision.EncodeImageAsync(path,CancellationToken.None);
    var red=vision.EncodeText("a red square"); var blue=vision.EncodeText("a blue square");
    if(image.Length!=512||red.Length!=512||LocalEmbeddings.Similarity(image,red)<=LocalEmbeddings.Similarity(image,blue)) throw new Exception("CLIP visual ranking failed.");
    Console.WriteLine("PASS: real CLIP image/text inference and color ranking; generated fixture only.");
    var portrait=Path.Combine(root,"portrait.png"); using(var bitmap=new Bitmap(128,512)) { using(var graphics=Graphics.FromImage(bitmap)) { graphics.Clear(Color.Blue); graphics.FillRectangle(Brushes.Red,0,128,128,256); } bitmap.Save(portrait,System.Drawing.Imaging.ImageFormat.Png); }
    var centered=await vision.EncodeImageAsync(portrait,CancellationToken.None);
    if(LocalEmbeddings.Similarity(centered,red)<=LocalEmbeddings.Similarity(centered,blue)) throw new Exception("Portrait vision center-crop failed.");
    Console.WriteLine("PASS: native portrait resizing and center-crop preprocessing.");
    var hindi=Path.Combine(root,"hindi.png"); using(var bitmap=new Bitmap(1000,200)) { using(var graphics=Graphics.FromImage(bitmap)) { graphics.Clear(Color.White); using var font=new Font("Nirmala UI",48); graphics.DrawString("नमस्ते भारत",font,Brushes.Black,20,40); } bitmap.Save(hindi,System.Drawing.Imaging.ImageFormat.Png); }
    ImageOcr.WindowsLanguage="auto"; var result=await ImageOcr.ReadAsync(hindi,CancellationToken.None);
    if(result.Text.Count(c=>c is >= '\u0900' and <= '\u097F')<3 || result.Words.Count==0) throw new Exception("Automatic bundled Hindi recognition failed: "+result.Text);
    Console.WriteLine("PASS: automatic mixed-language OCR recognizes generated Hindi with word bounds.");
    var pdfPath=Path.Combine(root,"hindi.pdf"); var builder=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
    using(var bitmap=new Bitmap(hindi)) using(var jpeg=new MemoryStream()) { bitmap.Save(jpeg,System.Drawing.Imaging.ImageFormat.Jpeg); builder.AddPage(600,200).AddJpeg(jpeg.ToArray(),new UglyToad.PdfPig.Core.PdfRectangle(0,0,600,200)); }
    await File.WriteAllBytesAsync(pdfPath,builder.Build()); var pdf=await PdfContent.ReadAsync(pdfPath,null,CancellationToken.None);
    if(pdf.OcrPages!=1||pdf.Text.Count(c=>c is >= '\u0900' and <= '\u097F')<3) throw new Exception("Multilingual scanned PDF failed: "+pdf.Text);
    ImageOcr.WindowsLanguage="";
    Console.WriteLine("PASS: generated scanned PDF recognizes Hindi using automatic multilingual OCR.");
    var texture=new byte[512*512]; new Random(51).NextBytes(texture);
    using var original=OpenCvSharp.Mat.FromPixelData(512,512,OpenCvSharp.MatType.CV_8UC1,texture);
    using var smooth=new OpenCvSharp.Mat(); OpenCvSharp.Cv2.GaussianBlur(original,smooth,new OpenCvSharp.Size(3,3),.8);
    var smoothed=new byte[texture.Length]; System.Runtime.InteropServices.Marshal.Copy(smooth.Data,smoothed,0,smoothed.Length);
    using var transform=OpenCvSharp.Cv2.GetPerspectiveTransform(new[] {new OpenCvSharp.Point2f(0,0),new OpenCvSharp.Point2f(511,0),new OpenCvSharp.Point2f(511,511),new OpenCvSharp.Point2f(0,511)},new[] {new OpenCvSharp.Point2f(30,-10),new OpenCvSharp.Point2f(510,30),new OpenCvSharp.Point2f(470,500),new OpenCvSharp.Point2f(-10,460)});
    using var edited=new OpenCvSharp.Mat(); OpenCvSharp.Cv2.WarpPerspective(smooth,edited,transform,new OpenCvSharp.Size(512,512));
    var warped=new byte[texture.Length]; System.Runtime.InteropServices.Marshal.Copy(edited.Data,warped,0,warped.Length);
    var first=new Wormy.Core.PhotoFingerprint(0,1,0,0,0,Features:GeometricPhotos.Extract(smoothed,512,512));
    var second=new Wormy.Core.PhotoFingerprint(0,1,0,0,0,Features:GeometricPhotos.Extract(warped,512,512));
    if(GeometricPhotos.Compare(first,second) is null) throw new Exception("Geometric perspective/crop fixture missed.");
    new Random(987).NextBytes(texture); var unrelatedImage=new Wormy.Core.PhotoFingerprint(0,1,0,0,0,Features:GeometricPhotos.Extract(texture,512,512));
    if(GeometricPhotos.Compare(first,unrelatedImage) is not null) throw new Exception("Unrelated geometry accepted.");
    Console.WriteLine("PASS: native geometric perspective/crop consistency and unrelated texture rejection.");
    var unsigned=Path.Combine(root,"unsigned.exe"); await File.WriteAllTextAsync(unsigned,"generated inert signature rejection fixture");
    try { VerifiedUpdates.VerifySignature(unsigned,new string('A',40)); throw new Exception("Unsigned installer accepted."); } catch(InvalidDataException) { }
    if(File.ReadAllText(unsigned)!="generated inert signature rejection fixture") throw new Exception("Signature check modified original.");
    Console.WriteLine("PASS: native WinVerifyTrust rejects unsigned installer without execution or original changes.");
    var firstPath=Path.Combine(root,"original.txt"); var secondPath=Path.Combine(root,"hard-link.txt"); await File.WriteAllTextAsync(firstPath,"hard-link allocation fixture");
    if(LinkFixture.CreateHardLinkW(secondPath,firstPath,IntPtr.Zero))
    {
        var entries=(await Wormy.Core.FolderScanner.ScanAsync(root,CancellationToken.None)).Files.Where(file=>file.Path==firstPath||file.Path==secondPath).ToArray();
        var one=Wormy.Core.FolderAllocation.Measure(entries.Take(1),CancellationToken.None); var both=Wormy.Core.FolderAllocation.Measure(entries,CancellationToken.None);
        if(both.Measured!=2||both.AllocatedBytes!=one.AllocatedBytes||both.LogicalBytes!=one.LogicalBytes*2) throw new Exception("Hard-link allocation identity failed.");
        Console.WriteLine("PASS: native file IDs deduplicate allocated bytes for generated hard links.");
    }
}
finally { Directory.Delete(root,true); }
internal static class LinkFixture
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
    public static extern bool CreateHardLinkW(string path,string existing,IntPtr security);
}
