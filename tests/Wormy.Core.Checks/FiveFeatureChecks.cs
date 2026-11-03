using System.Buffers.Binary;
using System.Text;
using Wormy.Core;
using Wormy.Infrastructure;
internal static class FiveFeatureChecks
{
    public static async Task RunAsync()
    {
        var root=Path.Combine(Path.GetTempPath(),"Wormy-five-"+Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"Empty")); Directory.CreateDirectory(Path.Combine(root,"Excluded"));
            var full=Directory.CreateDirectory(Path.Combine(root,"Full")).FullName; await File.WriteAllTextAsync(Path.Combine(full,"file.txt"),"fixture");
            var inventory=FolderInventory.Read(root,new ScanOptions { ExcludedFolderNames=["Excluded"] },CancellationToken.None);
            if(inventory.Folders.Count!=2 || !inventory.Folders.Single(f=>f.Name=="Empty").Empty || inventory.Folders.Single(f=>f.Name=="Full").Empty) throw new Exception("Empty/live folder inventory failed.");
            var image=Path.Combine(root,"renamed.png");
            var header=new byte[] {137,80,78,71,13,10,26,10}; var text=Encoding.Latin1.GetBytes("Software\0ShareX 17.0");
            using(var stream=File.Create(image)) { stream.Write(header); var length=new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(length,(uint)text.Length); stream.Write(length); stream.Write(Encoding.ASCII.GetBytes("tEXt")); stream.Write(text); stream.Write(new byte[4]); }
            if(!CaptureMetadata.IsCapture(image)) throw new Exception("Renamed capture metadata missed.");
            var entry=new FileEntry(image,"renamed.png",new FileInfo(image).Length,File.GetLastWriteTimeUtc(image)) { CaptureMetadata=true };
            if(!LibraryInsights.IsScreenshot(entry)) throw new Exception("Metadata-aware screenshot filtering failed.");
            var index=new LibraryIndex(Path.Combine(root,"library.db")); await index.ReplaceRootAsync(root,[entry],CancellationToken.None);
            if(!(await new LibraryIndex(Path.Combine(root,"library.db")).LoadAsync()).Files.Single().CaptureMetadata) throw new Exception("Capture metadata restart failed.");
            await File.WriteAllBytesAsync(image,header);
            if(CaptureMetadata.IsCapture(image)) throw new Exception("Truncated metadata accepted.");
            var exif=new byte[50]; Encoding.ASCII.GetBytes("Exif\0\0").CopyTo(exif,0); exif[6]=73; exif[7]=73; exif[8]=42;
            BinaryPrimitives.WriteUInt32LittleEndian(exif.AsSpan(10,4),8); BinaryPrimitives.WriteUInt16LittleEndian(exif.AsSpan(14,2),1);
            BinaryPrimitives.WriteUInt16LittleEndian(exif.AsSpan(16,2),0x0131); BinaryPrimitives.WriteUInt16LittleEndian(exif.AsSpan(18,2),2);
            var software=Encoding.ASCII.GetBytes("Snipping Tool\0"); BinaryPrimitives.WriteUInt32LittleEndian(exif.AsSpan(20,4),(uint)software.Length); BinaryPrimitives.WriteUInt32LittleEndian(exif.AsSpan(24,4),26); software.CopyTo(exif,32);
            var jpeg=Path.Combine(root,"renamed.jpg"); using(var stream=File.Create(jpeg)) { stream.Write(new byte[] {255,216,255,225,0,(byte)(exif.Length+2)}); stream.Write(exif); stream.Write(new byte[] {255,217}); }
            if(!CaptureMetadata.IsCapture(jpeg)) throw new Exception("JPEG software metadata missed.");
            var detail=new byte[32*32*4];
            for(var y=0;y<32;y++) for(var x=0;x<32;x++) { var i=(y*32+x)*4; detail[i]=(byte)((x*x+y*17)%256); detail[i+1]=(byte)((x*9+y*y)%256); detail[i+2]=(byte)((x*19+y*7)%256); detail[i+3]=255; }
            var rotated=new byte[detail.Length]; var crop=new byte[detail.Length];
            for(var y=0;y<32;y++) for(var x=0;x<32;x++) { Array.Copy(detail,(y*32+x)*4,rotated,(x*32+31-y)*4,4); Array.Copy(detail,((8+y/2)*32+8+x/2)*4,crop,(y*32+x)*4,4); }
            PhotoFingerprint Sample(byte[] data,double aspect)=>new(0,aspect,100,100,100,Variants:SimilarPhotos.Variants(data,aspect));
            if(SimilarPhotos.Distance(Sample(detail,2),Sample(rotated,.5)) is null || SimilarPhotos.Distance(Sample(detail,2),Sample(crop,2)) is null) throw new Exception("Rotated/center-crop variants failed.");
            var files=(await FolderScanner.ScanAsync(full,CancellationToken.None)).Files;
            var allocation=FolderAllocation.Measure(files,CancellationToken.None);
            if(allocation.Measured+allocation.Unavailable!=files.Count || File.ReadAllText(files[0].Path)!="fixture") throw new Exception("Allocation accounting/original preservation failed.");
            using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
            try { FolderAllocation.Measure(files,cancelled.Token); throw new Exception("Allocation ignored cancellation."); } catch(OperationCanceledException) { }
            Console.WriteLine("PASS: capture metadata persistence/truncation, live empty folders/exclusions, quarter-turn and center-crop variants, allocation accounting/cancellation/original preservation.");
        }
        finally { Directory.Delete(root,true); }
    }
}
